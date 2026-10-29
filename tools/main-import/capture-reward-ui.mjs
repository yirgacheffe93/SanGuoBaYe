import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadEngine } from './import.mjs';
import { scheduler } from './capture-m3-c.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const cases = [
  { name: 'ordinary-empty', tool: 0, force: 110, iq: 110, full: false },
  { name: 'ordinary-full', tool: 0, force: 110, iq: 110, full: true },
  ...[30, 31, 32].flatMap(tool => [
    { name: `tool-${tool}-empty`, tool, force: 106, iq: 106, full: false },
    { name: `tool-${tool}-full`, tool, force: 106, iq: 106, full: true },
    { name: `tool-${tool}-threshold`, tool, force: 105, iq: 105, full: false },
  ]),
];
const scenarios = [];
for (const scenario of cases) {
  const timers = scheduler();
  const engine = await loadEngine(path.join(root, '.reference/main'), { timers, restoreNativePromise: true });
  const c = engine.context, d = c.baye.data;
  let prepared = false, attempted = false, finished = false, before;
  const events = [];
  const snapshot = () => {
    const person = d.g_Persons[13], city = d.g_Cities[5];
    return { force: person.Force, iq: person.IQ, armsType: person.ArmsType, devotion: person.Devotion,
      equipment: Array.from(person.Equip), tools: Array.from({length: city.Tools}, (_, i) =>
        new DataView(c.Module.HEAPU8.buffer).getUint16(d.g_GoodsQueue._addr + (city.ToolQueue + i) * 2, true)) };
  };
  c.baye.hooks = {
    chooseGameEntry: () => 0,
    loadPeriod: () => { c._bayeLoadPeriod(1); return 1; },
    chooseActor: () => 0,
    tacticStageUser: () => {
      if (!prepared) {
        prepared = true; d.g_PlayerKing = 13;
        Object.assign(d.g_CityPos, { setx: 7, sety: 1, x: 0, y: 0 });
        c._bayePutToolInCity(5, scenario.tool, 0);
        const person = d.g_Persons[13]; person.Force = scenario.force; person.IQ = scenario.iq;
        person.Equip[0] = scenario.full ? 1 : 0; person.Equip[1] = scenario.full ? 2 : 0;
        before = snapshot();
      }
      return -1;
    },
    cityMakeCommand: ctx => { ctx.cityIndex = 5; ctx.commandIndex = 9; events.push('native-reward-command'); return -1; },
    willGiveTool: ctx => {
      assert.equal(attempted, false, '单场景不得重复施加赏赐');
      assert.equal(ctx.cityIndex, 5); assert.equal(ctx.personIndex, 13); assert.equal(ctx.toolIndex, scenario.tool);
      attempted = true; events.push('native-willGiveTool'); return -1;
    },
    getToolPropertyValue: () => { if (attempted) finished = true; return -1; },
    getPersonPropertyValue: () => { if (attempted) finished = true; return -1; },
    mainSystemMenu: () => 0,
  };
  for (let steps = 0; !finished && steps < 100000 && timers.now < 20000; steps++) {
    if (timers.now % 100 === 0) c._bayeSendKey(0x27);
    timers.step();
    if (attempted && d.g_Cities[5].Tools === 0) finished = true;
  }
  assert.ok(finished && attempted, `${scenario.name}原生菜单未完成`);
  const after = snapshot();
  scenarios.push({ scenario, before, after, accepted: after.tools.length < before.tools.length, events });
  console.log(`REWARD_UI ${scenario.name} accepted=${after.tools.length < before.tools.length}`);
}
const result = { schemaVersion: 1,
  source: JSON.parse(readFileSync(path.join(root, 'content/main/import-summary.json'))).source,
  boundaries: ['通过真实地图/命令/道具/人物菜单并注入确认键；cityMakeCommand仅指定赏赐入口，willGiveTool不改结果。',
    '属性、装备槽和城内道具是受控初始条件；实际阈值、装备和消耗由生产LargessMake/AddGoodsPerson执行。'], scenarios };
const target = path.join(root, 'content/main/golden-reward-ui.json');
const serialized = JSON.stringify(result, null, 2) + '\n';
if (process.argv.includes('--check')) assert.equal(readFileSync(target, 'utf8'), serialized);
else writeFileSync(target, serialized);
console.log(`REWARD_UI_GOLDEN_OK ${scenarios.length}`);
