import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadEngine } from './import.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const options = { reference: path.join(ROOT, '.reference/main'), output: path.join(ROOT, 'content/main/golden-m2c-c.json') };
for (let i = 2; i < process.argv.length; i += 2) {
  const key = process.argv[i].replace(/^--/, '');
  if (!Object.hasOwn(options, key) || !process.argv[i + 1]) throw new Error('参数：--reference <目录> --output <JSON 文件>');
  options[key] = path.resolve(process.argv[i + 1]);
}

function scheduler() {
  let now = 0, nextId = 0;
  const jobs = new Map();
  const add = (fn, ms, interval) => {
    const id = ++nextId, delay = Math.max(1, Number(ms) || 0);
    jobs.set(id, { id, fn, at: now + delay, interval: interval ? delay : 0 });
    return id;
  };
  return {
    Date: class extends Date { constructor(...args) { super(...(args.length ? args : [now])); } static now() { return now; } },
    setTimeout: (fn, ms) => add(fn, ms, false), clearTimeout: (id) => jobs.delete(id),
    setInterval: (fn, ms) => add(fn, ms, true), clearInterval: (id) => jobs.delete(id),
    step() {
      const job = [...jobs.values()].sort((a, b) => a.at - b.at || a.id - b.id)[0];
      assert.ok(job, '没有可执行的定时器');
      now = job.at;
      if (job.interval) job.at += job.interval; else jobs.delete(job.id);
      job.fn();
    },
    get now() { return now; },
  };
}

function cities(c) {
  const d = c.baye.data;
  return Array.from({ length: d.g_Cities.length }, (_, city) => {
    const item = d.g_Cities[city];
    return {
      city,
      people: Array.from({ length: item.Persons }, (_, i) => d.g_PersonsQueue[item.PersonQueue + i]),
      tools: Array.from({ length: item.Tools }, (_, i) => new DataView(c.Module.HEAPU8.buffer).getUint16(d.g_GoodsQueue._addr + (item.ToolQueue + i) * 2, true)),
    };
  });
}

function person(c, index) {
  const p = c.baye.data.g_Persons[index];
  return { index, Age: p.Age };
}

function clearOrders(c) {
  for (let i = 0; i < c.baye.data.g_OrderQueue.length; i += 1) c.baye.data.g_OrderQueue[i].OrderId = 255;
}

async function runAnnual(period, year) {
  const timers = scheduler();
  const engine = await loadEngine(options.reference, { timers, restoreNativePromise: true });
  const c = engine.context;
  let finished = false, prepared = false, before, after, finalRng;
  c.baye.hooks = {
    chooseGameEntry: () => finished ? 3 : 0,
    loadPeriod: () => { c._bayeLoadPeriod(period); return 1; },
    chooseActor: () => period === 1 ? 13 : 0,
    tacticStageUser: () => 0,
    mainSystemMenu: () => 0,
    tacticStage3: () => {
      assert.equal(prepared, false);
      clearOrders(c);
      c.baye.data.g_YearDate = year - 1;
      c.baye.data.g_MonthDate = 12;
      c._bayeSRand(1);
      before = cities(c);
      prepared = true;
      return -1;
    },
    tacticStage5: () => {
      after = cities(c);
      finalRng = c._bayeGetSeed() >>> 0;
      finished = true;
      return -1;
    },
  };
  for (let steps = 0; !finished && steps < 100000 && timers.now < 120000; steps += 1) {
    if (timers.now % 100 === 0) c._bayeSendKey(0x27);
    timers.step();
  }
  assert.ok(finished && after, `年度场景未完成: ${period}/${year}`);
  const people = after.flatMap((city, cityIndex) => city.people.filter((p) => !before[cityIndex].people.includes(p)).map((index) => ({ index, city: cityIndex })));
  const tools = after.flatMap((city, cityIndex) => city.tools.filter((_, i) => city.tools[i] !== before[cityIndex].tools[i]).map((raw, i) => ({ raw, city: cityIndex, queueIndex: i })));
  return {
    scenario: 'annual', fixture: '生产阶段3设置年末、清空AI订单并固定RNG；阶段5读取真实ConditionUpdate结果', period, year,
    before: { year: year - 1, month: 12 }, after: { year, month: 1, rng: finalRng }, people, tools,
    ages: [0, 48, 54, 160, 169].map((index) => person(c, index)).map(({ index, Age }) => ({ index, age: Age })),
  };
}

async function runTimeCount() {
  const timers = scheduler();
  const engine = await loadEngine(options.reference, { timers, restoreNativePromise: true });
  const c = engine.context;
  let finished = false, before, after;
  c.baye.hooks = {
    chooseGameEntry: () => finished ? 3 : 0,
    loadPeriod: () => { c._bayeLoadPeriod(1); return 1; }, chooseActor: () => 13,
    tacticStageUser: () => 0, mainSystemMenu: () => 0,
    tacticStage3: () => {
      clearOrders(c);
      const order = c.baye.data.g_OrderQueue[0];
      for (const field of Object.getOwnPropertyNames(order).filter((name) => !name.startsWith('_'))) order[field] = 0;
      order.OrderId = 14; order.Person = 13; order.City = 5; order.Object = 3; order.TimeCount = 5;
      c.baye.data.g_Cities[3].Belong = 14;
      c._bayeDeletePersonInCity(5, 13);
      before = { orderTimeCount: order.TimeCount, cities: cities(c) };
      return -1;
    },
    tacticStage4: () => {
      after = { orderTimeCount: c.baye.data.g_OrderQueue[0].TimeCount, orderId: c.baye.data.g_OrderQueue[0].OrderId, cities: cities(c) };
      finished = true;
      return -1;
    },
    tacticStage5: () => -1,
  };
  for (let steps = 0; !finished && steps < 100000 && timers.now < 120000; steps += 1) {
    if (timers.now % 100 === 0) c._bayeSendKey(0x27);
    timers.step();
  }
  assert.ok(finished && after);
  assert.equal(after.orderId, 255);
  assert.equal(after.cities[3].people.includes(13), true);
  return { scenario: 'nonzero-timecount', fixture: '生产阶段3注入TimeCount=5的移动订单；阶段4检查实际PolicyExec', before, after };
}

const annual = [await runAnnual(1, 191), await runAnnual(1, 192), await runAnnual(1, 194)];
const output = {
  schemaVersion: 1,
  kind: 'main-golden-m2c-c',
  source: JSON.parse(readFileSync(path.join(ROOT, 'content/main/import-summary.json'), 'utf8')).source,
  annual,
  timeCount: await runTimeCount(),
  specialTool: {
    status: 'not-captured',
    reason: '自动键盘驱动未可靠完成原生赏赐菜单，不生成伪造golden；UseFlag分支按原生C实现并另做C#规则验收。',
  },
};
writeFileSync(options.output, `${JSON.stringify(output, null, 2)}\n`);
console.log(`MAIN_M2C_C_GOLDEN_OK annual=${annual.map((item) => `${item.year}:${item.people.length}`).join(',')} timeCount=${output.timeCount.after.orderId} specialTool=not-captured`);
