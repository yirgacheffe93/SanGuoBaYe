import assert from 'node:assert/strict';
import inspector from 'node:inspector';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadEngine } from './import.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const options = { reference: path.join(ROOT, '.reference/main'), output: path.join(ROOT, 'docs/evidence/setgoods-diagnosis.json') };
for (let i = 2; i < process.argv.length; i += 2) {
  const key = process.argv[i].replace(/^--/, '');
  if (!Object.hasOwn(options, key) || !process.argv[i + 1]) throw new Error('参数：--reference <目录> --output <JSON 文件>');
  options[key] = path.resolve(process.argv[i + 1]);
}

// Offset/local mappings below are only valid for this exact, unmodified binary.
const SUPPORTED_WASM = 'b933531e6ee5dbc0b491d9c0874027d7c80bc8ab32ff846c379d0f08e446cf7e';
const BREAKPOINTS = new Map([
  [0x47b4, 'condition-loaded'],
  [0x484b, 'before-report'],
  [0x485d, 'after-report'],
  [0x4894, 'setgoods-loop'],
  [0x48dc, 'before-store16'],
  [0x48df, 'after-store16'],
]);

function scheduler() {
  let now = 0, nextId = 0;
  const jobs = new Map();
  const add = (fn, ms, interval) => {
    const id = ++nextId, delay = Math.max(1, Number(ms) || 0);
    jobs.set(id, { id, fn, at: now + delay, interval: interval ? delay : 0 });
    return id;
  };
  return {
    Date: class extends Date {
      constructor(...args) { super(...(args.length ? args : [now])); }
      static now() { return now; }
    },
    setTimeout: (fn, ms) => add(fn, ms, false),
    clearTimeout: (id) => jobs.delete(id),
    setInterval: (fn, ms) => add(fn, ms, true),
    clearInterval: (id) => jobs.delete(id),
    step() {
      const job = [...jobs.values()].sort((a, b) => a.at - b.at || a.id - b.id)[0];
      assert.ok(job, '执行尚未完成，但已经没有定时器');
      now = job.at;
      if (job.interval) job.at += job.interval;
      else jobs.delete(job.id);
      job.fn();
    },
    get now() { return now; },
  };
}

function record(value) {
  return Object.fromEntries(Object.getOwnPropertyNames(value).filter((name) => !name.startsWith('_'))
    .map((name) => [name, value[name]]));
}

function metadata(c, array) {
  const subdef = c._ValueDef_get_array_subdef(array._def);
  return { address: array._addr, length: array.length, elementType: c._ValueDef_get_type(subdef), elementBytes: c._ValueDef_get_size(subdef) };
}

function rawQueue(c, city) {
  const array = c.baye.data.g_GoodsQueue;
  const address = array._addr + city.ToolQueue * 2;
  // The production engine uses load16/store16. The bridge exposes this array as U8.
  const memory = new DataView(c.Module.HEAPU8.buffer);
  assert.ok(address >= 0 && address + city.Tools * 2 <= memory.byteLength);
  return {
    address,
    rawU16: Array.from({ length: city.Tools }, (_, i) => memory.getUint16(address + i * 2, true)),
    bytes: Array.from(c.Module.HEAPU8.slice(address, address + city.Tools * 2)),
    legacyBridgeItems: Array.from({ length: city.Tools }, (_, i) => array[city.ToolQueue + i]),
  };
}

function snapshot(c, scenario, stage) {
  const d = c.baye.data, city = d.g_Cities[scenario.city];
  const people = Array.from({ length: city.Persons }, (_, i) => d.g_PersonsQueue[city.PersonQueue + i]);
  return {
    stage, cityIndex: scenario.city, toolQueue: city.ToolQueue, tools: city.Tools,
    ...rawQueue(c, city), rng: c._bayeGetSeed() >>> 0,
    money: city.Money, food: city.Food,
    personIndices: people, actorPresent: people.includes(scenario.person),
    order: record(d.g_OrderQueue[0]),
    asyncify: { state: c.Asyncify.state, continuationPending: c.Asyncify.currData !== null },
  };
}

function postSync(session, method, params) {
  let result, failure;
  session.post(method, params, (error, value) => { failure = error; result = value; });
  if (failure) throw failure;
  assert.ok(result, `Inspector ${method} 未同步返回；不能在暂停点继续读无效的 scope`);
  return result;
}

function readLocals(session, frame) {
  const scope = frame.scopeChain.find((item) => item.type === 'local');
  const fields = postSync(session, 'Runtime.getProperties', { objectId: scope.object.objectId, ownProperties: true }).result;
  return Object.fromEntries(fields.filter((item) => item.value.description === 'i32').map((item) => {
    const fields = postSync(session, 'Runtime.getProperties', { objectId: item.value.objectId, ownProperties: true }).result;
    return [item.name, fields.find((field) => field.name === 'value').value.value];
  }));
}

async function runScenario(scenario, trace) {
  const timers = scheduler(), stages = [], instructions = [];
  let c, scriptId, debuggerError, injected = false, completed = false, monthly = false;
  const session = new inspector.Session();
  session.connect();
  if (trace) {
    postSync(session, 'Debugger.enable', {});
    session.on('Debugger.scriptParsed', ({ params }) => {
      if (params.scriptLanguage === 'WebAssembly') scriptId = params.scriptId;
    });
    session.on('Debugger.paused', ({ params }) => {
      try {
        const frame = params.callFrames[0], offset = frame.location.columnNumber;
        const event = BREAKPOINTS.get(offset);
        assert.ok(event, `未知断点 ${offset}`);
        const locals = readLocals(session, frame);
        const item = { event, offset: `0x${offset.toString(16)}`, locals, snapshot: snapshot(c, scenario, event) };
        const memory = new DataView(c.Module.HEAPU8.buffer);
        if (event === 'condition-loaded') item.condition = {
          candidate: locals.$var2, actor: locals.$var3, bole: locals.$var7,
          address: locals.$var8 + locals.$var2 * 4 + 1,
          rawBole: memory.getUint16(locals.$var8 + locals.$var2 * 4 + 1, true),
        };
        if (event.includes('store16')) item.write = {
          address: locals.$var15, value: locals.$var2,
          memoryValue: memory.getUint16(locals.$var15, true),
        };
        const previous = instructions.at(-1);
        if (previous?.event === event) {
          // Asyncify revisits the report call while rewinding. Keep the first
          // snapshot and count identical queue observations without verbose logs.
          assert.deepEqual(item.snapshot.rawU16, previous.snapshot.rawU16);
          previous.pauseCount = (previous.pauseCount ?? 1) + 1;
        } else instructions.push(item);
      } catch (error) { debuggerError = error; }
      finally { postSync(session, 'Debugger.resume', {}); }
    });
  }
  try {
    const engine = await loadEngine(options.reference, { timers, restoreNativePromise: true });
    c = engine.context;
    assert.equal(engine.source.wasmSha256, SUPPORTED_WASM, '生产 WASM 已改变，需要重新核对断点偏移和局部变量语义');
    if (trace) {
      assert.ok(scriptId);
      for (const offset of BREAKPOINTS.keys()) {
        const bp = postSync(session, 'Debugger.setBreakpoint', { location: { scriptId, lineNumber: 0, columnNumber: offset } });
        assert.equal(bp.actualLocation.columnNumber, offset, '断点发生位移');
      }
    }
    const d = c.baye.data;
    const queueMetadata = { goods: metadata(c, d.g_GoodsQueue), persons: metadata(c, d.g_PersonsQueue) };
    c.baye.hooks = {
      chooseGameEntry: () => monthly ? 3 : 0,
      loadPeriod: () => { c._bayeLoadPeriod(1); return 1; },
      chooseActor: () => scenario.player,
      tacticStageUser: () => 0,
      mainSystemMenu: () => monthly ? 2 : 0,
      tacticStage3: () => {
        assert.equal(injected, false, '不得执行第二个月命令');
        // Discard unrelated AI commands, restore the period, and isolate the search.
        // This is an explicit controlled fixture, not a UI acceptance golden.
        c._bayeLoadPeriod(1);
        d.g_PlayerKing = scenario.player;
        if (scenario.injectTool !== undefined) c._bayePutToolInCity(scenario.city, scenario.injectTool, 1);
        for (let i = 0; i < d.g_OrderQueue.length; i += 1) d.g_OrderQueue[i].OrderId = 255;
        c._bayeSRand(scenario.seed);
        stages.push(snapshot(c, scenario, 'S0-fixture-ready'));
        const order = d.g_OrderQueue[0];
        for (const field of Object.keys(record(order))) order[field] = 0;
        order.OrderId = 3; order.Person = scenario.person; order.City = scenario.city;
        c._bayeDeletePersonInCity(scenario.city, scenario.person);
        injected = true;
        stages.push(snapshot(c, scenario, 'S1-queue-injected'));
        return -1;
      },
      willExecuteOrder: () => -1,
      tacticStage4: () => {
        stages.push(snapshot(c, scenario, 'S4-policy-complete'));
        completed = true;
        return -1;
      },
      tacticStage5: () => {
        stages.push(snapshot(c, scenario, 'S5-monthly-complete'));
        monthly = true;
        return -1;
      },
    };
    let steps = 0, nextKeyAt = 0;
    while (!monthly && steps++ < 100000 && timers.now < 120000) {
      // Only input needed by boot/notes/reports; strategy menus use existing hooks.
      if (timers.now >= nextKeyAt) { c._bayeSendKey(0x27); nextKeyAt = timers.now + 100; }
      timers.step();
      if (debuggerError) throw debuggerError;
    }
    assert.ok(completed && monthly, `场景 ${scenario.name} 未完成，最后阶段 ${stages.at(-1)?.stage}`);
    const before = stages.find((item) => item.stage.startsWith('S0'));
    const queued = stages.find((item) => item.stage.startsWith('S1'));
    const after = stages.find((item) => item.stage.startsWith('S4'));
    assert.deepEqual(before.rawU16, [scenario.tool]);
    assert.equal(queued.actorPresent, false);
    assert.deepEqual(after.rawU16, [scenario.tool | 0x8000]);
    assert.deepEqual(after.legacyBridgeItems, [scenario.tool]);
    assert.equal(after.rng, scenario.finalRng);
    assert.equal(after.order.OrderId, 255);
    assert.equal(after.actorPresent, true);
    assert.equal(after.money, before.money);
    assert.equal(after.food, before.food);
    assert.deepEqual(stages.at(-1).rawU16, after.rawU16);
    if (trace) {
      const condition = instructions.find((item) => item.event === 'condition-loaded');
      assert.equal(condition.condition.bole, 0);
      assert.equal(condition.condition.rawBole, 0);
      assert.equal(condition.condition.candidate, scenario.tool);
      const pre = instructions.find((item) => item.event === 'before-store16');
      const post = instructions.find((item) => item.event === 'after-store16');
      assert.equal(pre.write.address, before.address);
      assert.equal(pre.write.memoryValue, scenario.tool);
      assert.equal(pre.write.value, scenario.tool | 0x8000);
      assert.equal(post.write.memoryValue, scenario.tool | 0x8000);
      assert.ok(instructions.some((item) => item.event === 'before-report'));
      assert.ok(instructions.some((item) => item.event === 'after-report'));
    }
    return { ...scenario, trace, completed, monthlyCompleted: monthly, saveReloadTested: false,
      fixture: '生产引擎阶段 hook 注入命令；阶段 3 重载原始时期以清除 AI 干扰；不是 UI 接受测试',
      queueMetadata, stages, instructions, source: engine.source };
  } finally { session.disconnect(); }
}

async function auditImport() {
  const engine = await loadEngine(options.reference);
  const c = engine.context, imported = JSON.parse(readFileSync(path.join(ROOT, 'content/main/scenarios.json'), 'utf8'));
  const periods = [];
  for (let period = 1; period <= 4; period += 1) {
    c._bayeLoadPeriod(period);
    const differences = [];
    for (let cityIndex = 0; cityIndex < c.baye.data.g_Cities.length; cityIndex += 1) {
      const city = c.baye.data.g_Cities[cityIndex], raw = rawQueue(c, city);
      const expectedEntries = raw.rawU16.map((item) => ({ toolId: `tool-${String(item & 0x7fff).padStart(3, '0')}`, discovered: !!(item & 0x8000) }));
      const actualEntries = imported.periods[period - 1].cities[cityIndex].toolEntries;
      if (JSON.stringify(actualEntries) !== JSON.stringify(expectedEntries)) differences.push({ cityIndex, name: c.baye.getCityName(cityIndex), toolQueue: city.ToolQueue,
        ...raw, importedEntries: actualEntries, expectedEntries });
    }
    periods.push({ period, affectedCities: differences.length, differences });
  }
  return { goods: metadata(c, c.baye.data.g_GoodsQueue), persons: metadata(c, c.baye.data.g_PersonsQueue),
    affectedCityRecords: periods.reduce((sum, item) => sum + item.affectedCities, 0), periods };
}

const scenarios = [
  { name: 'A-pingyuan-injected', city: 5, person: 89, player: 13, tool: 0, injectTool: 0, seed: 3, finalRng: 679304702 },
  { name: 'B-beiping-original', city: 1, person: 16, player: 16, tool: 31, seed: 43, finalRng: 1008917062 },
];
const result = { kind: 'setgoods-runtime-diagnosis', binaryModified: false,
  method: '原始生产 JS/bridge/WASM，虚拟定时器按到期顺序推进；原有阶段 hook；可选 Node Inspector 只读断点',
  cases: [], importAudit: null };
for (const scenario of scenarios) {
  for (const [trace, player] of [[false, scenario.player], [false, 0], [true, scenario.player]]) {
    result.cases.push(await runScenario({ ...scenario, player, name: `${scenario.name}-${player === scenario.player ? 'player' : 'nonplayer'}${trace ? '-trace' : ''}` }, trace));
  }
}
result.importAudit = await auditImport();
mkdirSync(path.dirname(options.output), { recursive: true });
writeFileSync(options.output, `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify({ cases: result.cases.map((item) => ({ name: item.name,
  before: item.stages[0].rawU16, after: item.stages.find((stage) => stage.stage.startsWith('S4')).rawU16,
  legacyAfter: item.stages.find((stage) => stage.stage.startsWith('S4')).legacyBridgeItems })),
  affectedCityRecords: result.importAudit.affectedCityRecords, output: options.output }, null, 2));
