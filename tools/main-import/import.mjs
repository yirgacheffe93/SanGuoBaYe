import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';
import vm from 'node:vm';

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(SCRIPT_DIR, '../..');
const require = createRequire(import.meta.url);
const OUTPUT_FILES = [
  'content.json',
  'scenarios.json',
  'golden-liubei-190.json',
  'battle.json',
  'import-summary.json',
];
const CITY_FIELDS = [
  'State', 'Belong', 'SatrapId', 'FarmingLimit', 'Farming',
  'CommerceLimit', 'Commerce', 'PeopleDevotion', 'AvoidCalamity',
  'PopulationLimit', 'Population', 'Money', 'Food', 'MothballArms',
  'PersonQueue', 'Persons', 'ToolQueue', 'Tools',
];
const PERSON_FIELDS = [
  'OldBelong', 'Belong', 'Level', 'Force', 'IQ', 'Devotion',
  'Character', 'Experience', 'Thew', 'ArmsType', 'Arms', 'Tool1',
  'Tool2', 'Equip', 'Age',
];

function parseArguments(argv) {
  const options = {
    reference: path.join(REPO_ROOT, '.reference/main'),
    output: path.join(REPO_ROOT, 'content/main'),
    check: false,
  };
  for (let i = 0; i < argv.length; i += 1) {
    const argument = argv[i];
    if (argument === '--check') {
      options.check = true;
    } else if (argument === '--reference' || argument === '--output') {
      const value = argv[i + 1];
      if (!value) throw new Error(`${argument} 需要路径参数`);
      options[argument.slice(2)] = path.resolve(value);
      i += 1;
    } else {
      throw new Error(`未知参数: ${argument}`);
    }
  }
  return options;
}

function sha256(data) {
  return createHash('sha256').update(data).digest('hex');
}

function serialize(value) {
  return `${JSON.stringify(value, null, 2)}\n`;
}

function entityId(kind, index) {
  return `${kind}-${String(index).padStart(3, '0')}`;
}

function ownFields(record) {
  return Object.getOwnPropertyNames(record).filter(
    (field) => field && !field.startsWith('_'),
  );
}

function readValue(value) {
  if (typeof value === 'number' || typeof value === 'string' || value === null) {
    return value;
  }
  if (value?._type === 5 || value?._type === 7) {
    return Array.from({ length: value.length }, (_, index) => readValue(value[index]));
  }
  if (value?._type === 4 || value instanceof Object) {
    return Object.fromEntries(ownFields(value).map((field) => [field, readValue(value[field])]));
  }
  throw new Error(`无法导出反射值: ${String(value)}`);
}

function readFields(record, expectedFields) {
  const actualFields = ownFields(record);
  if (expectedFields) {
    const missing = expectedFields.filter((field) => !actualFields.includes(field));
    if (missing.length) throw new Error(`反射字段缺失: ${missing.join(', ')}`);
  }
  return Object.fromEntries(actualFields.map((field) => [field, readValue(record[field])]));
}

function personReference(rawId) {
  return rawId === 0 || rawId === 0xff ? null : entityId('person', rawId - 1);
}

function toolReference(rawId) {
  return rawId === 0 || rawId === 0xff ? null : entityId('tool', rawId - 1);
}

function readGoodsQueueU16(context, queueIndex) {
  const queue = context.baye.data.g_GoodsQueue;
  const address = queue._addr + queueIndex * 2;
  const heap = context.Module.HEAPU8;
  if (!Number.isInteger(queueIndex) || queueIndex < 0 || address + 2 > heap.length) {
    throw new Error(`道具队列地址越界: index=${queueIndex}, address=${address}`);
  }
  return new DataView(heap.buffer).getUint16(address, true);
}

function loadResource(library, resourceId) {
  const headerBytes = 14;
  const directoryOffset = (resourceId - 1) * 4;
  if (!Number.isInteger(resourceId) || resourceId < 1 || directoryOffset + 4 > library.length) {
    throw new Error(`资源编号越界: ${resourceId}`);
  }
  const address = library.readUInt32LE(directoryOffset);
  if (address + headerBytes > library.length) throw new Error(`资源头越界: ${resourceId}`);
  const bytes = library.readUInt32LE(address);
  const actualResourceId = library.readUInt16LE(address + 4);
  const itemCount = library.readUInt16LE(address + 6);
  const itemBytes = library.readUInt32LE(address + 8);
  if (actualResourceId !== resourceId) {
    throw new Error(`资源编号不一致: 目录 ${resourceId}，资源头 ${actualResourceId}`);
  }
  if (bytes < headerBytes || address + bytes > library.length) {
    throw new Error(`资源长度无效: ${resourceId}`);
  }
  const payloadBytes = bytes - headerBytes;
  if (itemBytes === 0) {
    const directory = address + headerBytes;
    if (directory + itemCount * 8 > address + bytes) throw new Error(`变长资源目录越界: ${resourceId}`);
    return {
      resourceId, address, itemCount, itemBytes,
      item(index) {
        if (!Number.isInteger(index) || index < 1 || index > itemCount) throw new Error(`资源 ${resourceId} 索引非法`);
        const start = address + library.readUInt32LE(directory + (index - 1) * 8);
        const length = library.readUInt32LE(directory + (index - 1) * 8 + 4);
        if (start + length > address + bytes) throw new Error(`资源 ${resourceId} 内容越界`);
        return library.subarray(start, start + length);
      },
    };
  }
  if (payloadBytes % itemBytes !== 0) throw new Error(`资源时期块不对齐: ${resourceId}`);
  return {
    resourceId,
    address,
    itemCount,
    itemBytes,
    itemCount: payloadBytes / itemBytes,
    item(period) {
      if (!Number.isInteger(period) || period < 1 || period > this.itemCount) {
        throw new Error(`资源 ${resourceId} 时期越界: ${period}`);
      }
      const start = address + headerBytes + (period - 1) * itemBytes;
      return library.subarray(start, start + itemBytes);
    },
  };
}

function readSearchConditions(library, resourceId, period, expectedCount) {
  const resource = loadResource(library, resourceId);
  if (resource.itemCount !== 4 || resource.itemBytes !== expectedCount * 4) {
    throw new Error(`SearchCondition 资源布局无效: ${JSON.stringify(resource)}`);
  }
  const data = resource.item(period);
  return Array.from({ length: expectedCount }, (_, index) => {
    const offset = index * 4;
    return {
      index,
      birth: data.readUInt8(offset),
      bole: data.readUInt16LE(offset + 1),
      city: data.readUInt8(offset + 3),
    };
  });
}

function readMap(library) {
  const resource = loadResource(library, 2);
  const constants = resource.item(14);
  const width = 12, height = 9;
  const cells = Array.from(constants);
  if (cells.length !== width * height) throw new Error('地图数据不足');
  const positions = Array.from({ length: 38 }, (_, index) => {
    const cell = cells.indexOf(index + 1);
    if (cell < 0) throw new Error(`地图缺少城市 ${index}`);
    return { cityId: entityId('city', index), x: cell % width, y: Math.floor(cell / width) };
  });
  if (new Set(cells.filter(Boolean)).size !== 38) throw new Error('地图城市编号重复或缺失');
  return { resourceId: 2, constantIndex: 14, width, height, cells, positions };
}

function readConnections(library) {
  return [59, 60].map((resourceId) => {
    const resource = loadResource(library, resourceId);
    if (resource.itemCount !== 1 || resource.itemBytes !== 38 * 16) {
      throw new Error(`城市连接资源布局无效: ${JSON.stringify(resource)}`);
    }
    const data = resource.item(1);
    return {
      resourceId,
      cities: Array.from({ length: 38 }, (_, index) => ({
        cityId: entityId('city', index),
        linkedCityIds: Array.from(data.subarray(index * 16, index * 16 + 8)).filter(Boolean).map((id) => entityId('city', id - 1)),
        directionalCityIds: Array.from(data.subarray(index * 16, index * 16 + 8)).map((id) => id ? entityId('city', id - 1) : null),
        distances: Array.from(data.subarray(index * 16 + 8, index * 16 + 16)),
      })),
    };
  });
}

function readBattleContent(library, source) {
  const constants = loadResource(library, 2);
  const cityMapIds = constants.item(15);
  const positions = constants.item(5);
  if (positions.length < 9 * 20) throw new Error('战斗初始站位表不足9组');
  const baseSkills = constants.item(2);
  const attackRangeData = constants.item(1);
  const resistanceData = constants.item(3);
  const landModifierData = constants.item(4);
  const skillRangeData = loadResource(library, 13).item(1);
  if (attackRangeData.length < 6 * 25 || resistanceData.length < 6 * 46 || landModifierData.length < 6 * 8 || skillRangeData.length < 30 * 81) {
    throw new Error('战斗范围或地形常量长度不足');
  }
  const terrainKind = (tile) => {
    if (tile > 15) return tile === 41 ? 6 : 7;
    if (tile > 5) return 2;
    if (tile > 4) return 3;
    if (tile > 3) return 4;
    if (tile > 2) return 5;
    if (tile > 1) return 0;
    return 1;
  };
  const maps = Array.from({ length: 38 }, (_, index) => {
    const resourceId = 110 + cityMapIds[index];
    const data = loadResource(library, resourceId).item(1);
    const width = data[0], height = data[2];
    const terrains = Array.from(data.subarray(16, 16 + width * height));
    const cityCell = terrains.indexOf(3);
    if (cityCell < 0 || terrains.indexOf(3, cityCell + 1) >= 0) throw new Error(`战斗地图城市格异常: ${resourceId}`);
    if (terrains.some((tile) => tile >= 46)) throw new Error(`战斗地图图块越界: ${resourceId}`);
    return { cityId: entityId('city', index), mapResourceId: resourceId, width, height,
      cityX: cityCell % width, cityY: Math.floor(cityCell / width), terrains, terrainKinds: terrains.map(terrainKind) };
  });
  return {
    schemaVersion: 1,
    kind: 'main-battle-content',
    source,
    maps,
    initialPositions: Array.from({ length: 9 }, (_, way) => Array.from(positions.subarray(way * 20, way * 20 + 20))),
    attackRanges: Array.from({ length: 6 }, (_, arm) => Array.from(attackRangeData.subarray(arm * 25, arm * 25 + 25))),
    movementResistance: Array.from({ length: 6 }, (_, arm) => Array.from(resistanceData.subarray(arm * 46, arm * 46 + 46))),
    landFightModifiers: Array.from({ length: 6 }, (_, arm) => Array.from(landModifierData.subarray(arm * 8, arm * 8 + 8), (value) => value > 127 ? value - 256 : value)),
    skillRanges: Array.from({ length: 30 }, (_, skill) => Array.from(skillRangeData.subarray(skill * 81, skill * 81 + 81))),
    unitSkills: Array.from({ length: 6 }, (_, arm) => Array.from(baseSkills.subarray(arm * 10, arm * 10 + 10)).filter(Boolean).map((id) => entityId('skill', id - 1))),
    unitSkillCounts: [3, 4, 3, 3, 4, 9],
    specialSkills: Array.from({ length: 4 }, (_, period) => {
      const data = loadResource(library, 14).item(period + 1);
      return Array.from({ length: 200 }, (_, index) => {
        const id = data.readUInt16LE(index * 2);
        return id ? entityId('skill', id - 1) : null;
      });
    }),
  };
}

export async function loadEngine(referenceDir, { timers, restoreNativePromise = false } = {}) {
  const jsDir = path.join(referenceDir, 'js');
  const libraryPath = path.join(referenceDir, 'libs/SGBY.lib');
  for (const requiredPath of [
    path.join(jsDir, 'baye.js'),
    path.join(jsDir, 'baye.wasm'),
    path.join(jsDir, 'bridge.js'),
    libraryPath,
  ]) {
    if (!existsSync(requiredPath)) throw new Error(`参考文件不存在: ${requiredPath}`);
  }

  const library = readFileSync(libraryPath);
  const engineMessages = [];
  const engineConsole = {
    log: (...args) => engineMessages.push(args.join(' ')),
    warn: (...args) => engineMessages.push(args.join(' ')),
    error: (...args) => engineMessages.push(args.join(' ')),
  };
  const context = {
    console: engineConsole,
    process,
    require,
    TextDecoder,
    TextEncoder,
    Uint8Array,
    ArrayBuffer,
    DataView,
    Int8Array,
    Int16Array,
    Int32Array,
    Uint16Array,
    Uint32Array,
    Float32Array,
    Float64Array,
    setTimeout: timers?.setTimeout ?? (() => 0),
    clearTimeout: timers?.clearTimeout ?? (() => {}),
    setInterval: timers?.setInterval ?? (() => 0),
    clearInterval: timers?.clearInterval ?? (() => {}),
    __dirname: jsDir,
    module: { exports: {} },
    exports: {},
    localStorage: {},
    lcdBlur: () => {},
    bayeResizeScreen: () => {},
  };
  context.window = context;
  context.global = context;
  if (timers?.Date) context.Date = timers.Date;
  vm.createContext(context);

  let mapCapture = null;
  const ready = new Promise((resolve, reject) => {
    context.Module = {
      noInitialRun: true,
      onRuntimeInitialized() {
        try {
          const bridge = readFileSync(path.join(jsDir, 'bridge.js'), 'utf8');
          vm.runInContext(bridge, context, { filename: 'bridge.js' });
          context._bayeSetLcdSize(160, 96);
          context.baye_bridge_init();
          // The legacy bridge replaces Promise; Asyncify.whenDone needs native Promise.
          if (restoreNativePromise) context.Promise = Promise;
          context.baye.drawMap = (width, height, matrix) => {
            mapCapture = {
              available: true,
              width,
              height,
              tiles: Array.from(matrix),
            };
          };
          context.bayeLoadFileContent = (filename) => (
            filename === 'baye//data/dat.lib' ? library.toString('hex') : ''
          );
          context.bayeFlushLcdBuffer = () => {};
          context.bayeStart = () => {};
          context.bayeExit = () => {};
          context._main();
          context._bayeGameEnvInit();
          resolve();
        } catch (error) {
          reject(error);
        }
      },
    };
  });

  const engine = readFileSync(path.join(jsDir, 'baye.js'), 'utf8');
  vm.runInContext(engine, context, { filename: 'baye.js' });
  await ready;

  const engineSource = readFileSync(path.join(jsDir, 'baye.js'));
  const bridgeSource = readFileSync(path.join(jsDir, 'bridge.js'));
  const wasmSource = readFileSync(path.join(jsDir, 'baye.wasm'));
  const commit = execFileSync('git', ['-C', referenceDir, 'rev-parse', 'HEAD'], {
    encoding: 'utf8',
  }).trim();

  return {
    context,
    library,
    takeMap() {
      const result = mapCapture ?? {
        available: false,
        reason: '仅加载时期不会触发地图绘制；未伪造地图矩阵或城市坐标。',
      };
      mapCapture = null;
      return result;
    },
    source: {
      branch: 'main',
      commit,
      library: 'libs/SGBY.lib',
      libraryBytes: library.length,
      librarySha256: sha256(library),
      engine: 'js/baye.js',
      engineSha256: sha256(engineSource),
      bridge: 'js/bridge.js',
      bridgeSha256: sha256(bridgeSource),
      wasm: 'js/baye.wasm',
      wasmSha256: sha256(wasmSource),
      decoding: 'Node.js 26 native TextDecoder(GBK)',
      engineMessages,
    },
  };
}

function extractPeriod(context, library, period, takeMap) {
  context._bayeLoadPeriod(period);
  const actualPeriod = context._bayeGetCurrentPeriod();
  if (actualPeriod !== period) {
    throw new Error(`时期加载失败: 请求 ${period}，实际 ${actualPeriod}`);
  }

  const data = context.baye.data;
  const personCount = context._bayeGetPersonCount();
  const cityCount = data.g_Cities.length;
  const nativeMap = readMap(library);
  const nativeConnections = readConnections(library);
  const cities = Array.from({ length: cityCount }, (_, index) => {
    const record = data.g_Cities[index];
    const raw = readFields(record, CITY_FIELDS);
    const nativePosition = nativeMap?.positions[index];
    const connections = nativeConnections?.map((table) => ({
      resourceId: table.resourceId,
      linkedCityIds: table.cities[index].linkedCityIds,
      directionalCityIds: table.cities[index].directionalCityIds,
      distances: table.cities[index].distances,
    }));
    const personIds = Array.from({ length: raw.Persons }, (_, offset) => {
      const personIndex = data.g_PersonsQueue[raw.PersonQueue + offset];
      return entityId('person', personIndex);
    });
    const rawToolQueue = Array.from({ length: raw.Tools }, (_, offset) => (
      readGoodsQueueU16(context, raw.ToolQueue + offset)
    ));
    const toolEntries = rawToolQueue.map((rawTool) => ({
      toolId: entityId('tool', rawTool & 0x7fff),
      discovered: (rawTool & 0x8000) !== 0,
    }));
    const city = {
      id: entityId('city', index),
      index,
      name: context.baye.getCityName(index),
      raw,
      ownerPersonId: personReference(raw.Belong),
      satrapPersonId: personReference(raw.SatrapId),
      personIds,
      toolIds: toolEntries.map((entry) => entry.toolId),
      toolEntries,
      nativePosition,
      connections,
    };
    Object.defineProperty(city, '_rawToolQueue', { value: rawToolQueue });
    return city;
  });

  const persons = Array.from({ length: personCount }, (_, index) => {
    const raw = readFields(data.g_Persons[index], PERSON_FIELDS);
    return {
      id: entityId('person', index),
      index,
      name: context.baye.getPersonName(index),
      raw,
      oldOwnerPersonId: personReference(raw.OldBelong),
      ownerPersonId: personReference(raw.Belong),
      tool1Id: toolReference(raw.Tool1),
      tool2Id: toolReference(raw.Tool2),
    };
  });

  const tools = Array.from({ length: 33 }, (_, index) => ({
    id: entityId('tool', index),
    index,
    name: context.baye.getToolName(index),
    raw: readFields(data.g_Tools[index]),
  }));
  const skills = Array.from({ length: 30 }, (_, index) => ({
    id: entityId('skill', index),
    index,
    name: context.baye.getSkillName(index),
    raw: readFields(data.g_Skills[index]),
  }));
  const orderQueue = Array.from({ length: data.g_OrderQueue.length }, (_, index) => ({
    id: entityId('order-slot', index),
    index,
    raw: readFields(data.g_OrderQueue[index]),
  }));

  return {
    id: entityId('period', period),
    period,
    date: { year: data.g_YearDate, month: data.g_MonthDate },
    map: takeMap(),
    cities,
    persons,
    personSearchConditions: readSearchConditions(library, 63, period, personCount),
    tools,
    toolSearchConditions: readSearchConditions(library, 67, period, tools.length),
    skills,
    orderQueue,
  };
}

function validateAndSummarize(content, scenarios, golden, checks) {
  function check(condition, id, details) {
    if (!condition) throw new Error(`校验失败 [${id}]: ${JSON.stringify(details)}`);
    checks.push({ id, result: 'passed', details });
  }

  check(scenarios.periods.length === 4, 'period-count', { actual: scenarios.periods.length, expected: 4 });
  check(content.tools.length === 33, 'tool-count', { actual: content.tools.length, expected: 33 });
  check(content.skills.length === 30, 'skill-count', { actual: content.skills.length, expected: 30 });
  const highBitProbe = new DataView(new Uint8Array([0x1f, 0x80]).buffer).getUint16(0, true);
  check(highBitProbe === 0x801f && (highBitProbe & 0x7fff) === 31 && (highBitProbe & 0x8000) !== 0,
    'u16le-high-bit-decoding', { value: highBitProbe });

  const referenceErrors = [];
  const periodSummaries = [];
  for (const period of scenarios.periods) {
    check(period.cities.length === 38, `period-${period.period}-city-count`, { actual: period.cities.length, expected: 38 });
    check(period.persons.length === 200, `period-${period.period}-person-count`, { actual: period.persons.length, expected: 200 });
    check(period.personSearchConditions.length === 200, `period-${period.period}-person-search-condition-count`, { actual: period.personSearchConditions.length, expected: 200 });
    check(period.tools.length === 33, `period-${period.period}-tool-count`, { actual: period.tools.length, expected: 33 });
    check(period.toolSearchConditions.length === 33, `period-${period.period}-tool-search-condition-count`, { actual: period.toolSearchConditions.length, expected: 33 });
    check(period.skills.length === 30, `period-${period.period}-skill-count`, { actual: period.skills.length, expected: 30 });

    for (const [kind, records] of Object.entries({
      city: period.cities,
      person: period.persons,
      tool: period.tools,
      skill: period.skills,
    })) {
      for (const record of records) {
        if (!record.name && (kind !== 'person' || record.raw.Level > 0)) {
          referenceErrors.push(`${period.id}/${record.id}: active record has empty name`);
        }
        if (record.id !== entityId(kind, record.index)) {
          referenceErrors.push(`${period.id}/${record.id}: unstable index mapping`);
        }
      }
    }
    for (const [kind, conditions] of Object.entries({
      person: period.personSearchConditions,
      tool: period.toolSearchConditions,
    })) {
      conditions.forEach((condition, index) => {
        if (condition.index !== index) {
          referenceErrors.push(`${period.id}/${kind}SearchConditions[${index}]: unstable index`);
        }
        for (const [field, maximum] of [['birth', 0xff], ['bole', 0xffff], ['city', 0xff]]) {
          if (!Number.isInteger(condition[field]) || condition[field] < 0 || condition[field] > maximum) {
            referenceErrors.push(`${period.id}/${kind}SearchConditions[${index}].${field}: invalid integer`);
          }
        }
      });
    }

    for (const city of period.cities) {
      if (!city.nativePosition || city.connections?.length !== 2 || city.connections.some((table) => table.distances.length !== 8)) {
        referenceErrors.push(`${period.id}/${city.id}: missing native position or connection tables`);
      }
      const linked = city.connections?.[0].linkedCityIds ?? [];
      if (new Set(linked).size !== linked.length) referenceErrors.push(`${period.id}/${city.id}: duplicate connection`);
      for (const target of linked) {
        const other = period.cities[Number(target.slice(-3))];
        if (!other?.connections?.[0].linkedCityIds.includes(city.id)) {
          referenceErrors.push(`${period.id}/${city.id}: asymmetric right connection to ${target}`);
        }
      }
      for (const [field, rawId] of [['Belong', city.raw.Belong], ['SatrapId', city.raw.SatrapId]]) {
        if (![0, 0xff].includes(rawId) && (rawId < 1 || rawId > period.persons.length)) {
          referenceErrors.push(`${period.id}/${city.id}.${field}: ${rawId}`);
        }
      }
      if (city.raw.PersonQueue + city.raw.Persons > 2000) {
        referenceErrors.push(`${period.id}/${city.id}: person queue out of range`);
      }
      if (city.raw.ToolQueue + city.raw.Tools > 2000) {
        referenceErrors.push(`${period.id}/${city.id}: tool queue out of range`);
      }
      for (const id of city.personIds) {
        const index = Number(id.slice(-3));
        if (index >= period.persons.length) referenceErrors.push(`${period.id}/${city.id}: ${id}`);
      }
      for (const id of city.toolIds) {
        const index = Number(id.slice(-3));
        if (index >= period.tools.length) referenceErrors.push(`${period.id}/${city.id}: ${id}`);
      }
    }

    const rawToolQueue = period.cities.flatMap((city) => city._rawToolQueue);
    const actualToolIds = period.cities.flatMap((city) => (
      city.toolEntries.map((entry) => entry.toolId)
    ));
    const compatibleToolIds = period.cities.flatMap((city) => city.toolIds);
    const expectedToolIds = rawToolQueue.map((rawTool) => entityId('tool', rawTool & 0x7fff));
    check(
      JSON.stringify(actualToolIds) === JSON.stringify(expectedToolIds)
        && JSON.stringify(compatibleToolIds) === JSON.stringify(actualToolIds),
      `period-${period.period}-tool-queue-order`,
      { entries: actualToolIds.length },
    );
    const duplicateCount = (values) => values.length - new Set(values).size;
    const actualDuplicateEntries = period.cities.reduce((sum, city) => (
      sum + duplicateCount(city.toolEntries.map((entry) => entry.toolId))
    ), 0);
    const expectedDuplicateEntries = period.cities.reduce((sum, city) => (
      sum + duplicateCount(city._rawToolQueue.map((rawTool) => rawTool & 0x7fff))
    ), 0);
    check(
      actualDuplicateEntries === expectedDuplicateEntries
        && actualToolIds.length === rawToolQueue.length,
      `period-${period.period}-tool-queue-duplicates`,
      { actual: actualDuplicateEntries, expected: expectedDuplicateEntries },
    );
    const actualDiscovered = period.cities.flatMap((city) => (
      city.toolEntries.map((entry) => entry.discovered)
    ));
    const expectedDiscovered = rawToolQueue.map((rawTool) => (rawTool & 0x8000) !== 0);
    check(
      JSON.stringify(actualDiscovered) === JSON.stringify(expectedDiscovered),
      `period-${period.period}-tool-queue-high-bit`,
      { discovered: actualDiscovered.filter(Boolean).length },
    );

    for (const person of period.persons) {
      for (const [field, rawId] of [['OldBelong', person.raw.OldBelong], ['Belong', person.raw.Belong]]) {
        if (![0, 0xff].includes(rawId) && (rawId < 1 || rawId > period.persons.length)) {
          referenceErrors.push(`${period.id}/${person.id}.${field}: ${rawId}`);
        }
      }
      for (const [field, rawId] of [['Tool1', person.raw.Tool1], ['Tool2', person.raw.Tool2]]) {
        if (![0, 0xff].includes(rawId) && (rawId < 1 || rawId > period.tools.length)) {
          referenceErrors.push(`${period.id}/${person.id}.${field}: ${rawId}`);
        }
      }
      for (const field of ['Level', 'Force', 'IQ', 'Devotion', 'Experience', 'Thew', 'Arms', 'Age']) {
        if (!Number.isInteger(person.raw[field]) || person.raw[field] < 0 || person.raw[field] > 0xff) {
          referenceErrors.push(`${period.id}/${person.id}.${field}: invalid U8 ${person.raw[field]}`);
        }
      }
    }

    const attributeFields = ['Force', 'IQ', 'Devotion', 'Thew', 'Arms'];
    const over100 = Object.fromEntries(attributeFields.map((field) => [
      field,
      period.persons.filter((person) => person.raw[field] > 100).length,
    ]));
    periodSummaries.push({
      id: period.id,
      date: period.date,
      cities: period.cities.length,
      persons: period.persons.length,
      personSearchConditions: period.personSearchConditions.length,
      tools: period.tools.length,
      toolSearchConditions: period.toolSearchConditions.length,
      skills: period.skills.length,
      orderSlots: period.orderQueue.length,
      peopleInCities: period.cities.reduce((sum, city) => sum + city.personIds.length, 0),
      toolsInCities: period.cities.reduce((sum, city) => sum + city.toolIds.length, 0),
      discoveredToolsInCities: actualDiscovered.filter(Boolean).length,
      duplicateToolQueueEntries: actualDuplicateEntries,
      attributeValuesOver100: over100,
      maxForce: Math.max(...period.persons.map((person) => person.raw.Force)),
      maxIQ: Math.max(...period.persons.map((person) => person.raw.IQ)),
      mapAvailable: period.map.available,
    });
  }
  check(referenceErrors.length === 0, 'reference-integrity', { errors: referenceErrors });

  const first = scenarios.periods[0];
  for (const period of scenarios.periods.slice(1)) {
    check(
      JSON.stringify(period.cities.map(({ id, name }) => ({ id, name })))
        === JSON.stringify(first.cities.map(({ id, name }) => ({ id, name }))),
      `period-${period.period}-city-identity-stability`,
      '城市原始索引与名称映射保持不变',
    );
  }

  const city = golden.city;
  check(golden.date.year === 190 && golden.date.month === 1, 'golden-date', golden.date);
  check(city.name === '平原' && city.personIds.length === 5, 'golden-pingyuan-people', {
    city: city.name,
    people: golden.people.map((person) => person.name),
  });
  check(
    city.raw.Farming === 938 && city.raw.FarmingLimit === 2470
      && city.raw.Commerce === 845 && city.raw.CommerceLimit === 2486
      && city.raw.PeopleDevotion === 68 && city.raw.Population === 32225
      && city.raw.PopulationLimit === 87096 && city.raw.Money === 93 && city.raw.Food === 234,
    'golden-pingyuan-values',
    city.raw,
  );
  const expectedForce = { 刘备: 85, 关羽: 106, 张飞: 107 };
  for (const [name, force] of Object.entries(expectedForce)) {
    const person = golden.people.find((candidate) => candidate.name === name);
    check(person?.raw.Force === force, `golden-force-${name}`, { actual: person?.raw.Force, expected: force });
  }
  check(
    golden.people.some((person) => person.raw.Force > 100),
    'golden-force-not-truncated',
    golden.people.map((person) => ({ name: person.name, force: person.raw.Force })),
  );
  const expectedTools = { 刘备: '双股剑', 关羽: '青龙刀', 张飞: '丈八矛' };
  for (const [personName, toolName] of Object.entries(expectedTools)) {
    const person = golden.people.find((candidate) => candidate.name === personName);
    const tool = golden.tools.find((candidate) => candidate.id === person?.tool1Id);
    check(tool?.name === toolName, `golden-tool-${personName}`, {
      actual: tool?.name,
      expected: toolName,
    });
  }
  for (const name of ['吴懿', '丁奉']) {
    const person = golden.people.find((candidate) => candidate.name === name);
    const condition = first.personSearchConditions[person.index];
    check(condition?.bole === 0, `golden-search-condition-${name}-bole`, {
      index: person.index,
      actual: condition?.bole,
      expected: 0,
    });
  }

  return periodSummaries;
}

function buildOutputs(engine) {
  const periods = [1, 2, 3, 4].map((period) => (
    extractPeriod(engine.context, engine.library, period, engine.takeMap)
  ));
  const first = periods[0];
  const source = { ...engine.source };
  delete source.engineMessages;
  const shared = {
    schemaVersion: 1,
    source,
    indexing: {
      city: 'index 为原始零基数组索引；nativePosition 来自资源2的C_MAP；connections 原样保留资源59/60的8个一基城市连接和8个距离字段。',
      person: 'index 为原始零基数组索引；Belong/OldBelong/SatrapId 保留原始一基值，0 与 0xff 的规范化为 null。',
      tool: 'index 为原始零基数组索引；Tool1/Tool2 保留原始一基值，0 与 0xff 的规范化为 null。',
      searchCondition: 'index 为资源内原始零基索引；birth/city 为 U8，bole 为 U16LE；city 保留原值且 fixCityOffset=0。',
      toolQueue: '保留原始顺序与重复项；toolId 使用 raw & 0x7fff 的零基索引，discovered 为 (raw & 0x8000) != 0。',
      orderQueue: '保留全部原始槽位及原始字段，不推断空槽位或字段规则。',
    },
  };
  const content = {
    ...shared,
    kind: 'main-content',
    idMappings: {
      cities: first.cities.map(({ id, index, name }) => ({ id, index, name })),
      persons: first.persons.map(({ id, index, name }) => ({ id, index, name })),
      tools: first.tools.map(({ id, index, name }) => ({ id, index, name })),
      skills: first.skills.map(({ id, index, name }) => ({ id, index, name })),
    },
    tools: first.tools,
    skills: first.skills,
  };
  const scenarios = { ...shared, kind: 'main-scenarios', periods };
  const pingyuan = first.cities.find((city) => city.name === '平原');
  if (!pingyuan) throw new Error('时期 1 未找到平原');
  const people = pingyuan.personIds.map((id) => {
    const person = first.persons.find((candidate) => candidate.id === id);
    if (!person) throw new Error(`平原人物引用不存在: ${id}`);
    return person;
  });
  const ruler = people.find((person) => person.name === '刘备');
  if (!ruler) throw new Error('平原未找到刘备');
  const equippedToolIds = [...new Set(people.flatMap((person) => (
    [person.tool1Id, person.tool2Id].filter(Boolean)
  )))];
  const equippedTools = equippedToolIds.map((id) => first.tools.find((tool) => tool.id === id));
  const golden = {
    schemaVersion: 1,
    kind: 'main-golden-liubei-190',
    source,
    scenarioId: first.id,
    date: first.date,
    ruler,
    city: pingyuan,
    people,
    tools: equippedTools,
    guanYuFarming: {
      status: 'not-captured',
      reason: '本工作包只进行无 UI 的只读反射导出，未可靠驱动开垦接受、取消和月末流程，因此不生成这些状态。',
    },
  };

  const checks = [];
  const periodSummaries = validateAndSummarize(content, scenarios, golden, checks);
  const files = {
    'content.json': serialize(content),
    'scenarios.json': serialize(scenarios),
    'golden-liubei-190.json': serialize(golden),
    'battle.json': serialize(readBattleContent(engine.library, source)),
  };
  const summary = {
    schemaVersion: 1,
    kind: 'main-import-summary',
    source,
    counts: {
      periods: periods.length,
      citiesPerPeriod: 38,
      personsPerPeriod: 200,
      cityRecords: periods.reduce((sum, period) => sum + period.cities.length, 0),
      personRecords: periods.reduce((sum, period) => sum + period.persons.length, 0),
      personSearchConditionsPerPeriod: first.personSearchConditions.length,
      personSearchConditionRecords: periods.reduce((sum, period) => sum + period.personSearchConditions.length, 0),
      toolSearchConditionsPerPeriod: first.toolSearchConditions.length,
      toolSearchConditionRecords: periods.reduce((sum, period) => sum + period.toolSearchConditions.length, 0),
      tools: content.tools.length,
      skills: content.skills.length,
      orderSlotsPerPeriod: first.orderQueue.length,
    },
    periods: periodSummaries,
    golden: {
      file: 'golden-liubei-190.json',
      city: pingyuan.name,
      people: people.map((person) => person.name),
      farming: `${pingyuan.raw.Farming}/${pingyuan.raw.FarmingLimit}`,
      commerce: `${pingyuan.raw.Commerce}/${pingyuan.raw.CommerceLimit}`,
      population: `${pingyuan.raw.Population}/${pingyuan.raw.PopulationLimit}`,
      money: pingyuan.raw.Money,
      food: pingyuan.raw.Food,
    },
    checks,
    limitations: [
      '战略地图使用资源59/60连接和资源2的C_MAP坐标，不使用g_CityPos；显示层仍可采用重写布局。',
      '未驱动 UI 指令流，关羽开垦的接受、取消和月末 golden 未生成。',
      'g_OrderQueue 全槽位按原值导出，不根据 demos.js 推断规则。',
    ],
    files: Object.fromEntries(Object.entries(files).map(([name, text]) => [name, {
      bytes: Buffer.byteLength(text),
      sha256: sha256(text),
    }])),
  };
  files['import-summary.json'] = serialize(summary);
  return files;
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  if (Number(process.versions.node.split('.')[0]) < 26) {
    throw new Error(`需要 Node.js 26 或更新版本，当前为 ${process.version}`);
  }
  const engine = await loadEngine(options.reference);
  const files = buildOutputs(engine);
  if (options.check) {
    for (const name of OUTPUT_FILES) {
      const outputPath = path.join(options.output, name);
      if (!existsSync(outputPath)) throw new Error(`待校验文件不存在: ${outputPath}`);
      const actual = readFileSync(outputPath, 'utf8');
      if (actual !== files[name]) throw new Error(`重复导出结果不一致: ${outputPath}`);
    }
    console.log(`校验通过：${OUTPUT_FILES.length} 个文件与真实重复导出逐字节一致。`);
  } else {
    mkdirSync(options.output, { recursive: true });
    for (const name of OUTPUT_FILES) {
      writeFileSync(path.join(options.output, name), files[name], 'utf8');
    }
    console.log(`导出完成：${OUTPUT_FILES.map((name) => path.join(options.output, name)).join(', ')}`);
  }
  const summary = JSON.parse(files['import-summary.json']);
  console.log(JSON.stringify({ counts: summary.counts, checks: summary.checks.length }, null, 2));
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
}
