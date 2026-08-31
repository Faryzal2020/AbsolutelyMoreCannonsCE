/** Hand-written OpenAPI 3.1 document describing the agent-facing API. */

const json = (schema) => ({ content: { 'application/json': { schema } } });
const ok = (description, schema = { type: 'object' }) => ({ description, ...json(schema) });

export function openApiSpec() {
  return {
    openapi: '3.1.0',
    info: {
      title: 'AMC Turret Editor API',
      version: '1.0.0',
      description:
        'Local REST API for extracting RimWorld turret ThingDefs into SQLite, editing them, '
        + 'and injecting the differences back into the original XML files in place. '
        + 'Every endpoint is deterministic and browser-free so CLI and agent test harnesses can drive it.',
    },
    servers: [{ url: 'http://localhost:3000' }],
    tags: [
      { name: 'pipeline', description: 'Extract, diff, inject, rollback' },
      { name: 'turrets', description: 'Read and edit turret records' },
      { name: 'reporting', description: 'Metrics, audit and state export' },
    ],
    paths: {
      '/api/health': {
        get: { tags: ['reporting'], summary: 'Liveness probe and SQLite driver in use',
          responses: { 200: ok('Server is up') } },
      },
      '/api/extract': {
        post: {
          tags: ['pipeline'],
          summary: 'Scan Common/Defs/ThingDefs_Buildings and repopulate SQLite',
          description: 'Wipes the extracted tables and re-reads every XML file. Any pending edit that has not been injected is discarded.',
          responses: { 200: ok('Extraction summary with per-entity counts', {
            type: 'object',
            properties: {
              ok: { type: 'boolean' },
              durationMs: { type: 'integer' },
              counts: {
                type: 'object',
                properties: {
                  files: { type: 'integer' }, turrets: { type: 'integer' },
                  weapons: { type: 'integer' }, costs: { type: 'integer' },
                  modExtensions: { type: 'integer' }, warnings: { type: 'integer' },
                },
              },
              malformed: { type: 'array', items: { type: 'object' } },
            },
          }) },
        },
      },
      '/api/turrets': {
        get: {
          tags: ['turrets'],
          summary: 'List every extracted turret with stats, ballistics and extensions',
          parameters: [
            { name: 'category', in: 'query', schema: { type: 'string' }, description: 'Folder category, or "all"' },
            { name: 'search', in: 'query', schema: { type: 'string' }, description: 'Full-text match over defName, label, category, ballistics and ammo' },
            { name: 'modified', in: 'query', schema: { type: 'string', enum: ['true'] } },
            { name: 'warnings', in: 'query', schema: { type: 'string', enum: ['true'] } },
          ],
          responses: { 200: ok('Turret collection') },
        },
      },
      '/api/turrets/{defName}': {
        get: {
          tags: ['turrets'], summary: 'Fetch one turret record',
          parameters: [{ name: 'defName', in: 'path', required: true, schema: { type: 'string' } }],
          responses: { 200: ok('Turret record'), 404: ok('Unknown def') },
        },
        put: {
          tags: ['turrets'],
          summary: 'Patch a turret record in SQLite',
          description:
            'Body is a partial turret object; nested objects are deep-merged and arrays replaced. '
            + 'Identity fields (defName, filePath, parentName, category) are ignored. '
            + 'Example: {"stats":{"maxHitPoints":1234}}',
          parameters: [{ name: 'defName', in: 'path', required: true, schema: { type: 'string' } }],
          requestBody: { required: true, ...json({ type: 'object' }) },
          responses: { 200: ok('Updated turret record'), 404: ok('Unknown def') },
        },
      },
      '/api/turrets/{defName}/revert': {
        post: {
          tags: ['turrets'], summary: 'Discard edits for one turret',
          parameters: [{ name: 'defName', in: 'path', required: true, schema: { type: 'string' } }],
          responses: { 200: ok('Reverted record') },
        },
      },
      '/api/revert-all': {
        post: { tags: ['turrets'], summary: 'Discard all pending edits', responses: { 200: ok('Revert count') } },
      },
      '/api/restore': {
        post: {
          tags: ['turrets'], summary: 'Replace the live state from a snapshot',
          requestBody: { required: true, ...json({ type: 'object', properties: { turrets: { type: 'array', items: { type: 'object' } } }, required: ['turrets'] }) },
          responses: { 200: ok('Restore count') },
        },
      },
      '/api/diffs': {
        get: {
          tags: ['pipeline'],
          summary: 'Unified JSON diff of modified SQLite records versus the XML on disk',
          parameters: [{ name: 'defName', in: 'query', schema: { type: 'string' }, description: 'Restrict to one def' }],
          responses: { 200: ok('Diff report') },
        },
      },
      '/api/diffs/{defName}/xml': {
        get: {
          tags: ['pipeline'], summary: 'Side-by-side original vs pending XML for one def',
          parameters: [{ name: 'defName', in: 'path', required: true, schema: { type: 'string' } }],
          responses: { 200: ok('Original and updated def blocks') },
        },
      },
      '/api/inject': {
        post: {
          tags: ['pipeline'],
          summary: 'Write pending changes into the XML files, creating .xml.bak backups',
          description:
            'Only the affected tags are rewritten; all other bytes in each file are preserved. '
            + 'A backup is created once per file and holds the pristine pre-injection content.',
          requestBody: json({
            type: 'object',
            properties: {
              defNames: { type: 'array', items: { type: 'string' }, description: 'Restrict injection to these defs' },
              dryRun: { type: 'boolean', description: 'Compute the result without touching disk' },
            },
          }),
          responses: { 200: ok('Injection report listing applied and skipped changes') },
        },
      },
      '/api/rollback': {
        post: {
          tags: ['pipeline'],
          summary: 'Restore every .xml.bak over its .xml and re-extract',
          description: 'Restores every .xml.bak under Common/Defs, including backups created by other tooling. Use dryRun, or GET /api/backups, to preview first.',
          requestBody: json({ type: 'object', properties: { keepBackups: { type: 'boolean' }, dryRun: { type: 'boolean' } } }),
          responses: { 200: ok('Rollback report') },
        },
      },
      '/api/backups': {
        get: { tags: ['pipeline'], summary: 'List current .xml.bak files', responses: { 200: ok('Backup list') } },
      },
      '/api/metrics': {
        get: { tags: ['reporting'], summary: 'Dashboard counters', responses: { 200: ok('Metric counters') } },
      },
      '/api/audit': {
        get: { tags: ['reporting'], summary: 'Validation report across every turret', responses: { 200: ok('Audit report') } },
      },
      '/api/export': {
        get: {
          tags: ['reporting'],
          summary: 'Download the turret matrix as CSV or an Excel workbook',
          description:
            'format=csv returns a single table chosen with `dataset`. format=xlsx returns Turret Systems, '
            + 'Resource Costs and Audit Findings as three sheets in one workbook. Both carry the same '
            + 'human-readable column headers.',
          parameters: [
            { name: 'format', in: 'query', schema: { type: 'string', enum: ['csv', 'xlsx'], default: 'csv' } },
            { name: 'dataset', in: 'query', description: 'CSV only', schema: { type: 'string', enum: ['turrets', 'summary', 'costs', 'audit'], default: 'turrets' } },
            { name: 'scope', in: 'query', schema: { type: 'string', enum: ['all', 'modified'], default: 'all' } },
          ],
          responses: {
            200: {
              description: 'Spreadsheet file',
              content: {
                'text/csv': { schema: { type: 'string' } },
                'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': { schema: { type: 'string', format: 'binary' } },
              },
            },
            400: ok('Unknown format or dataset'),
          },
        },
      },
      '/api/export/columns': {
        get: {
          tags: ['reporting'],
          summary: 'Column catalogue for an export dataset',
          parameters: [{ name: 'dataset', in: 'query', schema: { type: 'string', enum: ['turrets', 'summary', 'costs', 'audit'], default: 'turrets' } }],
          responses: { 200: ok('Headers, types and groups for every column') },
        },
      },
      '/api/schema': {
        get: {
          tags: ['reporting'],
          summary: 'Editable field catalogue with the XML path each field maps to',
          responses: { 200: ok('Field catalogue') },
        },
      },
    },
  };
}
