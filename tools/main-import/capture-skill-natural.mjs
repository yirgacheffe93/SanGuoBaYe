import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { run } from './capture-m3-c.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const content = JSON.parse(readFileSync(path.join(root, 'content/main/content.json')));
const battle = JSON.parse(readFileSync(path.join(root, 'content/main/battle.json')));
const period = JSON.parse(readFileSync(path.join(root, 'content/main/scenarios.json'))).periods[0];
const results = [];
for (const skill of content.skills) {
  const raw = skill.raw;
  let position;
  const range = battle.skillRanges[skill.index];
  let selectedMap;
  for (const map of battle.maps) {
  const city = period.cities.find(city => city.id === map.cityId);
  if (!city.ownerPersonId || city.ownerPersonId === period.cities[0].ownerPersonId || !city.personIds.length) continue;
  if (position) break;
  selectedMap = map;
  for (let y = 0; y < map.height && !position; y++)
  for (let x = 0; x < map.width && !position; x++)
  for (let ry = 0; ry < 9 && !position; ry++)
  for (let rx = 0; rx < 9 && !position; rx++) {
    const tx = x + rx - 4, ty = y + ry - 4;
    if (tx < 0 || ty < 0 || tx >= map.width || ty >= map.height) continue;
    const self = rx === 4 && ry === 4;
    if (range[ry * 9 + rx] !== 1 && !(self && range[ry * 9 + rx] === 2)) continue;
    if (raw.aim === 0 && self) continue;
    if (skill.index !== 29 && (!raw.oland[map.terrainKinds[y * map.width + x]] || !raw.eland[map.terrainKinds[ty * map.width + tx]])) continue;
    position = { actorX: x, actorY: y, targetX: tx, targetY: ty, targetUnit: raw.aim === 1 ? (self ? 10 : 11) : 0 };
  }
  }
  // 谍报没有普通目标地形/兵种条件，生产以自身施放。
  if (skill.index === 29) position = { actorX: 5, actorY: 5, targetX: 5, targetY: 5, targetUnit: 10 };
  assert.ok(position, `${skill.name}缺少合法位置`);
  for (const hit of [true, false]) {
    const scenario = {
      name: `${skill.id}-${hit ? 'hit' : 'miss'}`, skillSeed: hit ? 1 : 3, action: 'skill', natural: true, skillId: skill.index + 1,
      source: 0, target: Number(selectedMap.cityId.slice(-3)), player: period.cities.find(city => city.id === selectedMap.cityId).raw.Belong - 1, attackers: [57, 58], actorMp: 250,
      actorIq: hit ? 200 : 0, targetIq: hit ? 0 : 200,
      targetArmsType: raw.earm.findIndex(value => value > 0) < 0 ? 0 : raw.earm.findIndex(value => value > 0),
      targetArms: raw.aim === 1 ? 10 : 2000, targetState: skill.index === 24 ? 6 : 0,
      weather: raw.weather.findIndex(value => value > 0) + 1, ...position,
    };
    if (scenario.targetUnit === 10) { scenario.targetIq = scenario.actorIq; }
    // A self target shares IQ with the caster; use the observed natural result, not a forced hit label.
    const result = await run(scenario);
    const before = result.stages.find(stage => stage.stage === 'aiCommand');
    const after = result.stages.find(stage => stage.stage === 'stage4');
    results.push({ scenario, before, skillStart: result.stages.find(stage => stage.stage === 'beforeSkill'), after });
    console.log(`SKILL_NATURAL ${scenario.name} rng=${before.rng}->${after.rng}`);
  }
}
for (const actorOverflow of [true, false]) {
  const scenario = { name: actorOverflow ? 'byte-overflow-actor' : 'byte-overflow-target', action: 'skill', natural: true,
    skillId: 4, skillSeed: 3, source: 0, target: 1, player: 16, attackers: [57, 58], actorMp: 250,
    actorIq: actorOverflow ? 255 : 0, targetIq: actorOverflow ? 0 : 255, targetArms: 2000,
    actorX: 0, actorY: 0, targetX: 3, targetY: 0, targetUnit: 0, weather: 1 };
  const result = await run(scenario);
  results.push({ scenario, before: result.stages.find(stage => stage.stage === 'aiCommand'),
    skillStart: result.stages.find(stage => stage.stage === 'beforeSkill'), after: result.stages.find(stage => stage.stage === 'stage4') });
}
const output = { schemaVersion: 1, source: JSON.parse(readFileSync(path.join(root, 'content/main/import-summary.json'))).source,
  boundaries: ['30个技能各两组受控属性和固定seed场景，命中由生产RNG自然决定，不覆盖showSkill结果。',
    'skillStart记录概率判定前RNG；after的RNG包含生产动画/提示消耗，不宣称与无UI的规则层全量RNG相同。',
    '命令由aiFightCommand注入，不作为原版战术AI或玩家技能菜单的等价证据；stage5受控结束。'], scenarios: results };
const target = path.join(root, 'content/main/golden-skill-natural.json');
const serialized = JSON.stringify(output, null, 2) + '\n';
if (process.argv.includes('--check')) assert.equal(readFileSync(target, 'utf8'), serialized, '自然技能golden与重复捕获不一致');
else writeFileSync(target, serialized);
console.log(`SKILL_NATURAL_GOLDEN_OK ${results.length}`);
