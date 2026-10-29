import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadEngine } from './import.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const options = { reference: path.join(ROOT, '.reference/main'), output: path.join(ROOT, 'content/main/golden-m3-c.json') };
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
  const d = c.baye.data;
  return {
    rng: c._bayeGetSeed() >>> 0,
    over: d.g_FgtOver,
    weather: d.g_FgtWeather,
    day: d.g_FgtBoutCnt,
    params: Object.fromEntries(['Mode', 'Way', 'CityIndex', 'MapId', 'MProvender', 'EProvender'].map((key) => [key, d.g_FgtParam[key]])),
    genArray: Array.from(d.g_FgtParam.GenArray),
    units: Array.from({ length: 20 }, (_, i) => ({
      person: d.g_FgtParam.GenArray[i] - 1, x: d.g_GenPos[i].x, y: d.g_GenPos[i].y,
      hp: d.g_GenPos[i].hp, mp: d.g_GenPos[i].mp, move: d.g_GenPos[i].move,
      active: d.g_GenPos[i].active, state: d.g_GenPos[i].state,
    })).filter((unit) => unit.person >= 0),
    persons: [13, 56, 57, 78, 89, 90].map((index) => {
      const person = d.g_Persons[index];
      return { index, Belong: person.Belong, Arms: person.Arms, IQ: person.IQ, Level: person.Level, Experience: person.Experience };
    }),
    city: (() => {
      const city = d.g_Cities[4];
      return { Belong: city.Belong, Farming: city.Farming, Commerce: city.Commerce, Money: city.Money, Food: city.Food, PeopleDevotion: city.PeopleDevotion };
    })(),
  };
}

function injectOrder(c, scenario) {
  const d = c.baye.data;
  d.g_engineConfig.confirmOnEscape = 0;
  d.g_PlayerKing = scenario.player ?? 13;
  for (let i = 0; i < d.g_OrderQueue.length; i += 1) d.g_OrderQueue[i].OrderId = 255;
  for (let i = 0; i < d.FIGHTERS.length; i += 1) d.FIGHTERS[i] = 0;
  for (let i = 0; i < d.FIGHTERS_IDX.length; i += 1) d.FIGHTERS_IDX[i] = 0;
  const attackers = scenario.attackers ?? [14, 90, 91];
  const memory = new DataView(c.Module.HEAPU8.buffer);
  attackers.forEach((person, i) => { memory.setUint16(d.FIGHTERS._addr + i * 2, person, true); });
  if ((scenario.source ?? 5) === 5) {
    for (const person of [13, 89, 90]) c._bayeDeletePersonInCity(5, person);
  }
  const order = d.g_OrderQueue[0];
  for (const field of Object.getOwnPropertyNames(order).filter((name) => !name.startsWith('_'))) order[field] = 0;
  order.OrderId = 27;
  order.City = scenario.source ?? 5;
  order.Object = scenario.target ?? 4;
  order.Food = scenario.food ?? 100;
  order.Person = 0;
  c._bayeSRand(scenario.seed ?? 3);
}

async function run(scenario) {
  const timers = scheduler();
  const engine = await loadEngine(options.reference, { timers, restoreNativePromise: true });
  const c = engine.context, d = c.baye.data;
  const stages = [];
  let finished = false, positioned = false, battleRounds = 0, refreshCount = 0;
  const capture = (stage) => stages.push({ stage, ...snapshot(c) });
  c.baye.hooks = {
    chooseGameEntry: () => finished ? 3 : 0,
    loadPeriod: () => { c._bayeLoadPeriod(1); return 1; },
    chooseActor: () => 0,
    tacticStageUser: () => 0,
    mainSystemMenu: () => 0,
    tacticStage3: () => { c._bayeLoadPeriod(1); injectOrder(c, scenario); return -1; },
    enterBattle: () => { capture('enterBattle'); },
    battleStage1: () => { capture('stage1'); },
    battleStage2: () => {
      if (!positioned) {
        positioned = true;
        for (let i = 0; i < 10; i += 1) d.g_GenPos[i].active = 1;
      }
      if (!scenario.action) d.g_FgtOver = 2;
      if (scenario.action && d.g_GenPos[scenario.actor ?? 10].active === 0) {
        const attacker = scenario.actor ?? 10;
        const target = scenario.targetUnit ?? 0;
        d.g_GenPos[attacker].x = 5;
        d.g_GenPos[attacker].y = 5;
        d.g_GenPos[target].x = 6;
        d.g_GenPos[target].y = 5;
        d.g_MapSX = 0;
        d.g_MapSY = 0;
        d.g_FoucsX = 6;
        d.g_FoucsY = 5;
        if (scenario.targetArms !== undefined) d.g_Persons[d.g_FgtParam.GenArray[target] - 1].Arms = scenario.targetArms;
        if (scenario.actorIq !== undefined) d.g_Persons[d.g_FgtParam.GenArray[attacker] - 1].IQ = scenario.actorIq;
        if (scenario.targetIq !== undefined) d.g_Persons[d.g_FgtParam.GenArray[target] - 1].IQ = scenario.targetIq;
      }
      capture('stage2');
      if (scenario.action) c._bayeSendKey(0x28);
      return -1;
    },
    battleStage3: () => { capture('stage3'); },
    battleStage4: () => { capture('stage4'); },
    battleStage5: () => { battleRounds += 1; capture('stage5'); d.g_FgtOver = 2; },
    fightChooseAction: () => {
      if (scenario.action) {
        const target = d.g_GenPos[scenario.targetUnit ?? 10];
        d.g_FoucsX = target.x;
        d.g_FoucsY = target.y;
        c._bayeSendTouchEvent(1, target.x * 16 + 8, target.y * 16 + 8);
        c._bayeSendTouchEvent(2, target.x * 16 + 8, target.y * 16 + 8);
      }
      return scenario.action === 'attack' ? 0 : scenario.action === 'skill' || scenario.action === 'heal' || scenario.action === 'miss' ? 1 : 3;
    },
    fightChooseSkill: () => scenario.action === 'heal' ? 17 : 4,
    showSkill: (ctx) => { ctx.result = scenario.action === 'miss' ? 0 : 1; return 0; },
    fightOpenMainMenu: () => 0,
    aiFightCommand: (ctx) => {
      if (stages.some((stage) => stage.stage === 'aiCommand')) {
        ctx.type = 3;
        return 0;
      }
      capture('aiCommand');
      ctx.type = scenario.action === 'attack' ? 0 : 1;
      ctx.sIdx = scenario.actor ?? 10;
      ctx.aIdx = scenario.targetUnit ?? 0;
      ctx.skillId = scenario.action === 'heal' ? 17 : 4;
      stages.at(-1).command = { type: ctx.type, actorSlot: ctx.sIdx, targetSlot: ctx.aIdx, skillId: ctx.skillId };
      return 0;
    },
    exitBattle: () => { capture('exitBattle'); },
    tacticStage4: () => { capture('afterResolve'); finished = true; return -1; },
    tacticStage5: () => -1,
  };
  for (let steps = 0; !finished && steps < 100000 && timers.now < 120000; steps += 1) {
    if (timers.now % 100 === 0) c._bayeSendKey(0x27);
    timers.step();
  }
  assert.ok(finished, `场景未完成: ${scenario.name}，最后阶段 ${stages.at(-1)?.stage}`);
  if (!stages.some((stage) => stage.stage === 'enterBattle')) console.log(`M3_C_NO_ENTER ${scenario.name}: ${engine.source.engineMessages.join(' | ')}`);
  if (scenario.action) {
    for (const required of ['stage3', 'aiCommand', 'stage4']) assert.ok(stages.some((stage) => stage.stage === required), `${scenario.name} 缺少 ${required}`);
    const before = stages.find((stage) => stage.stage === 'aiCommand');
    const after = stages.find((stage) => stage.stage === 'stage4');
    const actorIndex = d.g_FgtParam.GenArray[scenario.actor ?? 10] - 1;
    const targetIndex = d.g_FgtParam.GenArray[scenario.targetUnit ?? 0] - 1;
    const actorBefore = before.persons.find((person) => person.index === actorIndex);
    const actorAfter = after.persons.find((person) => person.index === actorIndex);
    const targetAfter = after.persons.find((person) => person.index === targetIndex);
    const targetUnitAfter = after.units.find((unit) => unit.person === targetIndex);
    const expected = {
      attack: { arms: 90, hp: 91, state: 0, experience: 2, mp: 53 },
      skill: { arms: 0, hp: 91, state: 8, experience: 2, mp: 23 },
      miss: { arms: 100, hp: 91, state: 0, experience: 0, mp: 23 },
      heal: { arms: 810, hp: 94, state: 0, experience: 2, mp: 38 },
    }[scenario.action];
    assert.equal(targetAfter.Arms, expected.arms, `${scenario.name} 目标兵力`);
    assert.equal(targetUnitAfter.hp, expected.hp, `${scenario.name} 目标HP`);
    assert.equal(targetUnitAfter.state, expected.state, `${scenario.name} 目标状态`);
    assert.equal(actorAfter.Experience - actorBefore.Experience, expected.experience, `${scenario.name} 经验`);
    assert.equal(after.units.find((unit) => unit.person === actorIndex).mp, expected.mp, `${scenario.name} MP`);
  }
  return { ...scenario, fixture: '生产S3写入真实FIGHTERS/出征订单，经BattleDrv/GamFight；受控坐标与showSkill分支，伤害/状态/经验由生产引擎计算', stages };
}

const scenarios = [
  { name: 'initialize', action: null },
  { name: 'auto-npc', player: 13, source: 0, target: 4, attackers: [57, 58], seed: 3 },
  { name: 'normal-attack', action: 'attack', source: 0, target: 5, attackers: [57, 58], actorIq: 255, targetIq: 0 },
  { name: 'skill-hit', action: 'skill', source: 0, target: 5, attackers: [57, 58], actorIq: 255, targetIq: 0 },
  { name: 'skill-miss', action: 'miss', source: 0, target: 5, attackers: [57, 58], actorIq: 0, targetIq: 255 },
  { name: 'skill-heal', action: 'heal', source: 0, target: 5, attackers: [57, 58], actorIq: 255, targetUnit: 11, targetArms: 10, targetIq: 0 },
];
const results = [];
for (const scenario of scenarios) results.push(await run(scenario));
const output = {
  schemaVersion: 1,
  kind: 'main-golden-m3-c',
  source: JSON.parse(readFileSync(path.join(ROOT, 'content/main/import-summary.json'), 'utf8')).source,
  boundaries: [
    '初始化与动作场景在stage5受控结束，over=2不是生产自然胜负证据。',
    '技能命中/失败由生产showSkill钩子固定分支；MP、伤害、状态与经验仍由生产引擎计算，不作为自然概率golden。',
    '首个AI动作受控，其余AI武将待机；不作为战术AI等价证据。',
    'auto-npc不进入交互战场是生产FGT_AUTO路径，不是捕获失败。',
  ],
  scenarios: results,
};
writeFileSync(options.output, `${JSON.stringify(output, null, 2)}\n`);
console.log(`MAIN_M3_C_GOLDEN_OK scenarios=${results.map((item) => `${item.name}:${item.stages.at(-1).over}`).join(',')}`);
