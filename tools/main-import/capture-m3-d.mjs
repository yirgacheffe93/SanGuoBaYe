import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { run } from './capture-m3-c.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const scenarios = [
  { name: 'move-attack', action: 'move-attack', source: 0, target: 5, attackers: [57, 58], actorIq: 0, targetIq: 0, actorMp: 0, targetDistance: 3 },
  { name: 'wait', action: 'wait', source: 0, target: 5, attackers: [57, 58], actorMp: 52 },
  { name: 'skill-range', action: 'skill-range', source: 0, target: 5, attackers: [57, 58], actorIq: 255, targetIq: 0, targetDistance: 3 },
];
const results = [];
for (const scenario of scenarios) results.push(await run(scenario));
const output = {
  schemaVersion: 1,
  kind: 'main-golden-m3-d',
  source: JSON.parse(readFileSync(path.join(root, 'content/main/import-summary.json'), 'utf8')).source,
  boundaries: [
    'move-attack由生产FgtCmpMove和后续动作自然执行，actorMp=0用于排除技能分支。',
    'wait验证生产CMD_REST只恢复当前武将1点MP。',
    'skill-range使用突袭真实dx=3射程格；showSkill固定命中，不冒充自然概率证据。',
    '所有场景在stage5受控结束，over=2不是自然胜负证据。',
  ],
  scenarios: results,
};
writeFileSync(path.join(root, 'content/main/golden-m3-d.json'), `${JSON.stringify(output, null, 2)}\n`);
console.log(`MAIN_M3_D_GOLDEN_OK ${results.map((item) => item.name).join(',')}`);
