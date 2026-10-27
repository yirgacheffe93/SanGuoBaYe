import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadEngine } from './import.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const options = { reference: path.join(ROOT, '.reference/main'), output: path.join(ROOT, 'content/main/golden-m2d-a.json') };
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

function snapshot(c, people) {
  const d = c.baye.data;
  return {
    rng: c._bayeGetSeed() >>> 0,
    cities: Array.from({ length: d.g_Cities.length }, (_, index) => {
      const city = d.g_Cities[index];
      return { index, Belong: city.Belong, SatrapId: city.SatrapId,
        people: Array.from({ length: city.Persons }, (_, i) => d.g_PersonsQueue[city.PersonQueue + i]) };
    }),
    persons: people.map((index) => {
      const p = d.g_Persons[index];
      return { index, Belong: p.Belong, IQ: p.IQ, Devotion: p.Devotion, Character: p.Character,
        Thew: p.Thew, Tool1: p.Tool1, Tool2: p.Tool2 };
    }),
    order: Object.fromEntries(Object.getOwnPropertyNames(d.g_OrderQueue[0]).filter((name) => !name.startsWith('_')).map((name) => [name, d.g_OrderQueue[0][name]])),
  };
}

async function run(scenario) {
  const timers = scheduler();
  const engine = await loadEngine(options.reference, { timers, restoreNativePromise: true });
  const c = engine.context, d = c.baye.data;
  const actor = 89, target = scenario.target ?? 57, people = [...new Set([actor, target, 5, 58])];
  let finished = false, before, after;
  c.baye.hooks = {
    chooseGameEntry: () => finished ? 3 : 0,
    loadPeriod: () => { c._bayeLoadPeriod(1); return 1; }, chooseActor: () => 13,
    tacticStageUser: () => 0, mainSystemMenu: () => 0,
    tacticStage3: () => {
      for (let i = 0; i < d.g_OrderQueue.length; i += 1) d.g_OrderQueue[i].OrderId = 255;
      d.g_Persons[actor].IQ = scenario.success ? (scenario.command === 15 ? 200 : 255) : 0;
      d.g_Persons[target].IQ = scenario.success ? 0 : 255;
      d.g_Persons[target].Devotion = scenario.devotion ?? (scenario.success ? 0 : 100);
      if (scenario.command === 19) {
        d.g_Cities[1].Belong = 14;
        d.g_Cities[2].Belong = 14;
        d.g_Cities[4].Belong = 14;
        d.g_Cities[6].Belong = 14;
        if (scenario.success) {
          for (let i = 0; i < d.g_Cities.length; i += 1) {
            if (i !== 0 && i !== 3 && d.g_Cities[i].Belong === 6) d.g_Cities[i].Belong = 14;
          }
        }
        c._bayeDeletePersonInCity(3, 58);
      }
      if (scenario.command === 17) d.g_Cities[0].SatrapId = target + 1;
      const order = d.g_OrderQueue[0];
      for (const field of Object.getOwnPropertyNames(order).filter((name) => !name.startsWith('_'))) order[field] = 0;
      order.OrderId = scenario.command;
      order.Person = actor;
      order.City = 5;
      order.Object = scenario.command === 23 ? 0 : target;
      order.TimeCount = scenario.command === 23 ? 1 : 10;
      c._bayeDeletePersonInCity(5, actor);
      c._bayeSRand(scenario.rng ?? (scenario.success ? 7 : 1));
      before = snapshot(c, people);
      return -1;
    },
    tacticStage4: () => { after = snapshot(c, people); finished = true; return -1; },
    tacticStage5: () => -1,
  };
  for (let steps = 0; !finished && steps < 100000 && timers.now < 120000; steps += 1) {
    if (timers.now % 100 === 0) c._bayeSendKey(0x27);
    timers.step();
  }
  assert.ok(finished && after, `场景未完成: ${scenario.name}`);
  assert.equal(after.order.OrderId, 255, `订单未清槽: ${scenario.name}`);
  assert.equal(after.cities[5].people.includes(actor), true, `执行者未归队: ${scenario.name}`);
  return { ...scenario, fixture: '生产S3受控人物/城市/RNG与订单，S4读取真实PolicyExec；不是UI接受测试', before, after };
}

const scenarios = [
  { name: 'alienate-success-devotion-floor', command: 15, success: true, devotion: 3 },
  { name: 'alienate-first-gate-failure', command: 15, success: false },
  { name: 'canvass-success', command: 16, success: true },
  { name: 'canvass-success-loyalty-rng-zero', command: 16, success: true, rng: 63 },
  { name: 'canvass-first-gate-failure', command: 16, success: false },
  { name: 'counterespionage-success-independent', command: 17, success: true },
  { name: 'counterespionage-first-gate-failure', command: 17, success: false },
  { name: 'induce-success-whole-faction', command: 19, success: true, target: 5 },
  { name: 'induce-city-count-failure', command: 19, success: false, target: 5 },
  { name: 'reconnoitre-return', command: 23, success: true },
];
const results = [];
for (const scenario of scenarios) results.push(await run(scenario));
const output = {
  schemaVersion: 1,
  kind: 'main-golden-m2d-a',
  source: JSON.parse(readFileSync(path.join(ROOT, 'content/main/import-summary.json'), 'utf8')).source,
  acceptance: { money: [50, 50, 50, 50, 20], thew: [20, 20, 20, 20, 10], commands: [15, 16, 17, 19, 23],
    note: '来自main命令表；反间18的Make/Drv均为空实现，不生成自创玩法。' },
  scenarios: results,
};
writeFileSync(options.output, `${JSON.stringify(output, null, 2)}\n`);
const independent = results.find((item) => item.name === 'counterespionage-success-independent');
const induced = results.find((item) => item.name === 'induce-success-whole-faction');
console.log(`MAIN_M2D_A_GOLDEN_OK scenarios=${results.length} devotion=${results[0].after.persons.find((p) => p.index === 57).Devotion} independent=${independent.after.cities[0].Belong} induceBoundary=unchanged-rng-${induced.after.rng}`);
