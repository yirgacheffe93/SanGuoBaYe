import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadEngine } from './import.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const options = { reference: path.join(ROOT, '.reference/main'), output: path.join(ROOT, 'content/main/golden-m2d-b.json') };
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

function snapshot(c) {
  const d = c.baye.data, city = d.g_Cities[0];
  return {
    rng: c._bayeGetSeed() >>> 0,
    playerKing: d.g_PlayerKing,
    city: {
      State: city.State, Belong: city.Belong, SatrapId: city.SatrapId,
      Farming: city.Farming, Commerce: city.Commerce, PeopleDevotion: city.PeopleDevotion,
      AvoidCalamity: city.AvoidCalamity, Population: city.Population, Money: city.Money, Food: city.Food,
      people: Array.from({ length: city.Persons }, (_, i) => d.g_PersonsQueue[city.PersonQueue + i]),
    },
    persons: [56, 57].map((index) => {
      const p = d.g_Persons[index];
      return { index, Belong: p.Belong, Level: p.Level, Force: p.Force, IQ: p.IQ, Experience: p.Experience, Arms: p.Arms };
    }),
    orders: Array.from({ length: d.g_OrderQueue.length }, (_, i) => d.g_OrderQueue[i]).filter((order) => order.OrderId !== 255)
      .map((order) => Object.fromEntries(Object.getOwnPropertyNames(order).filter((name) => !name.startsWith('_')).map((name) => [name, order[name]]))),
  };
}

async function run(branch, seed) {
  const timers = scheduler();
  const engine = await loadEngine(options.reference, { timers, restoreNativePromise: true });
  const c = engine.context, d = c.baye.data;
  let finished = false, before, after;
  c.baye.hooks = {
    chooseGameEntry: () => finished ? 3 : 0,
    loadPeriod: () => { c._bayeLoadPeriod(1); return 1; }, chooseActor: () => 13,
    tacticStageUser: () => 0, mainSystemMenu: () => 0,
    tacticStage2: () => {
      for (let i = 0; i < d.g_OrderQueue.length; i += 1) d.g_OrderQueue[i].OrderId = 255;
      for (let i = 0; i < d.g_Cities.length; i += 1) {
        d.g_Cities[i].Belong = i === 0 ? 6 : i === 5 ? 14 : 0;
        d.g_Cities[i].SatrapId = 0;
      }
      Object.assign(d.g_Cities[0], {
        State: 2, Farming: 100, FarmingLimit: 1000, Commerce: 100, CommerceLimit: 1000,
        PeopleDevotion: 10, AvoidCalamity: 99, Population: 100, PopulationLimit: 1000,
        Money: 20000, Food: 50, MothballArms: 100,
      });
      d.g_PlayerKing = 13;
      c._bayeSRand(seed);
      before = snapshot(c);
      return -1;
    },
    tacticStage3: () => { after = snapshot(c); finished = true; return -1; },
    tacticStage4: () => -1, tacticStage5: () => -1,
  };
  for (let steps = 0; !finished && steps < 100000 && timers.now < 120000; steps += 1) {
    if (timers.now % 100 === 0) c._bayeSendKey(0x27);
    timers.step();
  }
  assert.ok(finished && after, `AI 场景未完成: ${branch}`);
  return { branch, seed, fixture: '生产S2隔离一个AI城和玩家城，清空订单并固定RNG；S3读取真实ComputerTactic，不是玩家UI接受测试', before, after };
}

const scenarios = [
  await run('interior-harmonize', 3),
  await run('diplomacy', 29),
  await run('diplomacy-order', 80),
  await run('armament', 7),
];
const output = {
  schemaVersion: 1,
  kind: 'main-golden-m2d-b',
  source: JSON.parse(readFileSync(path.join(ROOT, 'content/main/import-summary.json'), 'utf8')).source,
  boundaries: ['不捕获M3出征；AI直接效果与玩家公式分别记录。', '反间/劝降沿用Main-M2D-A的生产边界。'],
  scenarios,
};
writeFileSync(options.output, `${JSON.stringify(output, null, 2)}\n`);
console.log(`MAIN_M2D_B_GOLDEN_OK ${scenarios.map((item) => `${item.branch}:orders=${item.after.orders.length},rng=${item.after.rng}`).join(' ')}`);
