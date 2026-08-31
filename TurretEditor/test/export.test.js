import test from 'node:test';
import assert from 'node:assert/strict';
import zlib from 'node:zlib';
import { makeFixture, startApp } from './helpers.js';

const NAVAL = 'Turret_127mmMark16_Base';

/** Parse a CSV line, honouring RFC 4180 quoting. */
function parseCsvLine(line) {
  const out = [];
  let cur = '';
  let quoted = false;
  for (let i = 0; i < line.length; i++) {
    const c = line[i];
    if (quoted) {
      if (c === '"' && line[i + 1] === '"') { cur += '"'; i++; }
      else if (c === '"') quoted = false;
      else cur += c;
    } else if (c === '"') quoted = true;
    else if (c === ',') { out.push(cur); cur = ''; }
    else cur += c;
  }
  out.push(cur);
  return out;
}

function parseCsv(body) {
  const text = body.replace(/^﻿/, '');
  const lines = text.split('\r\n').filter((l) => l !== '');
  return { headers: parseCsvLine(lines[0]), rows: lines.slice(1).map(parseCsvLine) };
}

/** Read one stored/deflated member out of a ZIP archive by name. */
function readZipEntry(buffer, name) {
  const target = Buffer.from(name, 'utf8');
  for (let i = 0; i < buffer.length - 4; i++) {
    if (buffer.readUInt32LE(i) !== 0x04034b50) continue;
    const nameLen = buffer.readUInt16LE(i + 26);
    const extraLen = buffer.readUInt16LE(i + 28);
    const entryName = buffer.slice(i + 30, i + 30 + nameLen);
    if (!entryName.equals(target)) continue;
    const compSize = buffer.readUInt32LE(i + 18);
    const method = buffer.readUInt16LE(i + 8);
    const start = i + 30 + nameLen + extraLen;
    const data = buffer.slice(start, start + compSize);
    return method === 8 ? zlib.inflateRawSync(data) : data;
  }
  return null;
}

test('spreadsheet export', async (t) => {
  const fixture = makeFixture();
  const app = await startApp();
  t.after(async () => { await app.close(); fixture.cleanup(); });
  await app.post('/api/extract');

  await t.test('CSV carries readable headers and one row per turret', async () => {
    const res = await app.fastify.inject({ url: '/api/export?format=csv&dataset=turrets' });
    assert.equal(res.statusCode, 200);
    assert.match(res.headers['content-type'], /text\/csv/);
    assert.match(res.headers['content-disposition'], /amc_turret_matrix\.csv/);
    // Excel needs the BOM to read UTF-8 labels correctly.
    assert.ok(res.body.startsWith('﻿'), 'UTF-8 BOM present');

    const { headers, rows } = parseCsv(res.body);
    for (const expected of [
      'Def Name', 'Label', 'Category', 'Max Hit Points', 'Work To Build', 'Mass (kg)',
      'Turret Cooldown (s)', 'Resource Costs', 'Min Range (cells)', 'Max Range (cells)',
      'AmmoSet', 'Magazine Size', 'Power Draw (W)', 'Rotary Animation', 'Heat Threshold (shots)',
    ]) assert.ok(headers.includes(expected), `header "${expected}" present`);

    assert.equal(new Set(headers).size, headers.length, 'headers are unique');
    assert.ok(!headers.some((h) => /[._]/.test(h)), 'headers are prose, not raw keys');

    const { body: list } = await app.get('/api/turrets');
    assert.equal(rows.length, list.count, 'one row per turret');

    const row = rows.find((r) => r[headers.indexOf('Def Name')] === NAVAL);
    assert.ok(row);
    assert.equal(row[headers.indexOf('Max Hit Points')], '1792');
    assert.equal(row[headers.indexOf('Turret Cooldown (s)')], '3.3');
    assert.equal(row[headers.indexOf('Manned')], 'Yes');
    assert.equal(row[headers.indexOf('Power Draw (W)')], '500');
    assert.equal(row[headers.indexOf('Resource Costs')], 'Steel x900; ComponentIndustrial x20; Plasteel x30; ComponentSpacer x5');
  });

  await t.test('the summary table is a narrower slice of the same columns', async () => {
    const full = parseCsv((await app.fastify.inject({ url: '/api/export?format=csv&dataset=turrets' })).body);
    const summary = parseCsv((await app.fastify.inject({ url: '/api/export?format=csv&dataset=summary' })).body);
    assert.ok(summary.headers.length < full.headers.length);
    assert.equal(summary.rows.length, full.rows.length);
    for (const h of summary.headers) assert.ok(full.headers.includes(h), `${h} matches the full table`);
  });

  await t.test('the costs table is one row per resource line', async () => {
    const { headers, rows } = parseCsv((await app.fastify.inject({ url: '/api/export?format=csv&dataset=costs' })).body);
    assert.deepEqual(headers, ['Def Name', 'Label', 'Category', 'Fire Mode', 'Resource ThingDef', 'Count']);

    const { body: list } = await app.get('/api/turrets');
    const expected = list.turrets.reduce((n, x) => n + x.costs.length, 0);
    assert.equal(rows.length, expected);

    const steel = rows.find((r) => r[0] === NAVAL && r[4] === 'Steel');
    assert.equal(steel[5], '900');
  });

  await t.test('values containing commas are quoted', async () => {
    const res = await app.fastify.inject({ url: '/api/export?format=csv&dataset=turrets' });
    assert.ok(res.body.includes('"Steel x900; ComponentIndustrial x20; Plasteel x30; ComponentSpacer x5"') === false,
      'semicolon-joined costs need no quoting');
    // Building size is stored as "3,3" and must be quoted.
    assert.ok(/"\d+,\d+"/.test(res.body), 'comma-bearing values are quoted');
  });

  await t.test('scope=modified narrows the export', async () => {
    await app.put(`/api/turrets/${NAVAL}`, { stats: { bulk: 321 } });
    const { headers, rows } = parseCsv((await app.fastify.inject({ url: '/api/export?format=csv&scope=modified' })).body);
    assert.equal(rows.length, 1);
    assert.equal(rows[0][headers.indexOf('Def Name')], NAVAL);
    assert.equal(rows[0][headers.indexOf('Bulk')], '321');
    assert.equal(rows[0][headers.indexOf('Modified')], 'Yes');

    const res = await app.fastify.inject({ url: '/api/export?format=csv&scope=modified' });
    assert.match(res.headers['content-disposition'], /amc_turret_matrix_modified\.csv/);
    await app.post('/api/revert-all');
  });

  await t.test('the audit table lists one row per finding', async () => {
    await app.put(`/api/turrets/${NAVAL}`, { stats: { maxHitPoints: 0 } });
    const { headers, rows } = parseCsv((await app.fastify.inject({ url: '/api/export?format=csv&dataset=audit' })).body);
    assert.deepEqual(headers, ['Def Name', 'Label', 'Category', 'Severity', 'Rule', 'Finding', 'Def File']);
    const hit = rows.find((r) => r[0] === NAVAL && r[4] === 'hp');
    assert.ok(hit, 'the zero-HP finding is exported');
    assert.equal(hit[3], 'Error');
    await app.post('/api/revert-all');
  });

  await t.test('xlsx is a valid workbook with three sheets', async () => {
    const res = await app.fastify.inject({ url: '/api/export?format=xlsx' });
    assert.equal(res.statusCode, 200);
    assert.match(res.headers['content-type'], /spreadsheetml\.sheet/);
    assert.match(res.headers['content-disposition'], /amc_turret_matrix\.xlsx/);

    const buffer = res.rawPayload;
    assert.equal(buffer.readUInt32LE(0), 0x04034b50, 'starts with a ZIP local file header');

    const workbook = readZipEntry(buffer, 'xl/workbook.xml').toString('utf8');
    for (const name of ['Turret Systems', 'Resource Costs', 'Audit Findings']) {
      assert.ok(workbook.includes(`name="${name}"`), `${name} sheet present`);
    }

    const sheet1 = readZipEntry(buffer, 'xl/worksheets/sheet1.xml').toString('utf8');
    assert.ok(sheet1.includes('<t>Max Hit Points</t>'), 'header written');
    assert.ok(sheet1.includes('state="frozen"'), 'header row frozen');
    assert.ok(sheet1.includes('<autoFilter'), 'filters enabled');
    // Numbers must be numeric cells, not inline strings, so Excel can total them.
    assert.ok(/<c r="[A-Z]+\d+"><v>1792<\/v><\/c>/.test(sheet1), 'MaxHitPoints stored as a number');
    assert.ok(sheet1.includes('t="inlineStr"><is><t>Turret_127mmMark16_Base</t>'), 'def name stored as text');

    assert.ok(readZipEntry(buffer, '[Content_Types].xml'), 'content types part present');
    assert.ok(readZipEntry(buffer, 'xl/styles.xml'), 'styles part present');
  });

  await t.test('the column catalogue backs the export dialog', async () => {
    const { body } = await app.get('/api/export/columns?dataset=turrets');
    assert.equal(body.ok, true);
    assert.ok(body.columns.length > 80);
    const hp = body.columns.find((c) => c.key === 'maxHitPoints');
    assert.equal(hp.header, 'Max Hit Points');
    assert.equal(hp.type, 'number');
    assert.equal(hp.group, 'Gameplay Stats');
    assert.ok(body.datasets.some((d) => d.id === 'costs'));

    const { body: summary } = await app.get('/api/export/columns?dataset=summary');
    assert.ok(summary.columns.length < body.columns.length);
  });

  await t.test('unknown formats and datasets are rejected', async () => {
    const badFormat = await app.get('/api/export?format=pdf');
    assert.equal(badFormat.status, 400);
    assert.match(badFormat.body.error, /Unsupported format/);

    const badDataset = await app.get('/api/export?format=csv&dataset=nope');
    assert.equal(badDataset.status, 400);
    assert.match(badDataset.body.error, /Unknown dataset/);
  });
});
