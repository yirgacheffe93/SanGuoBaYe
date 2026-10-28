import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const battle = JSON.parse(readFileSync(path.join(ROOT, 'content/main/battle.json'), 'utf8'));
const summary = JSON.parse(readFileSync(path.join(ROOT, 'content/main/import-summary.json'), 'utf8'));
assert.equal(battle.maps.length, 38);
assert.equal(battle.initialPositions.length, 9);
assert.equal(battle.specialSkills.length, 4);
const output = {
  schemaVersion: 1,
  kind: 'main-m3-a-import-evidence',
  source: summary.source,
  map: battle.maps[4],
  initialPositions: battle.initialPositions,
  unitSkills: battle.unitSkills,
  unitSkillCounts: battle.unitSkillCounts,
  people: [13, 89, 90].map((index) => ({ index, specialSkill: battle.specialSkills[0][index] })),
  runtimeBattle: {
    status: 'not-captured',
    reason: '生产GamFight需要先经真实AddFightOrder建立20槽，现有离线入口尚未可靠完成UI编队与战斗驱动；不把C#测试或导入数据冒充生产战斗golden。',
  },
};
writeFileSync(path.join(ROOT, 'content/main/golden-m3-a.json'), `${JSON.stringify(output, null, 2)}\n`);
console.log('MAIN_M3_A_IMPORT_EVIDENCE_OK maps=38 positions=9 runtimeBattle=not-captured');
