# AMC Turret Editor

A single local web application that extracts RimWorld turret `ThingDef` XML into SQLite, lets you edit it
in a dashboard, and writes the differences back into the original files in place.

It replaces the previous multi-step Python + static HTML workflow in `DevTools/`.

## Quick start

```bash
cd TurretEditor
npm install
npm start
```

`npm start` builds the dashboard if needed, finds a free port starting at 3000, serves the UI, and opens
`http://localhost:3000` in your default browser.

Other entry points:

| Command | What it does |
| --- | --- |
| `npm start` | Build UI if needed, serve, open a browser |
| `npm run serve` | Same, without opening a browser (CI / agents) |
| `npm run mcp` | Serve the same pipeline to AI clients over MCP (stdio) |
| `npm test` | Full integration suite against a throwaway copy of the mod XML |
| `npm run build` | Rebuild the dashboard bundle only |

## How it works

```
Common/Defs/ThingDefs_Buildings/**.xml
        │  POST /api/extract
        ▼
DevTools/turret_editor.db      ← edit through the UI or PUT /api/turrets/:defName
        │  POST /api/inject    (creates .xml.bak, rewrites only the changed tags)
        ▼
Common/Defs/ThingDefs_Buildings/**.xml
        │  POST /api/rollback  (restores every .xml.bak)
        ▼
original files
```

### Injection is surgical

RimWorld defs are hand-authored: they carry comments, mixed tabs and spaces, and deliberate ordering.
Parsing and re-serialising a file would destroy all of that, so injection never rewrites whole files.
A position-aware scanner (`src/xml/scanner.js`) locates the exact character range of the target tag and
replaces only its inner text. Everything else in the file — including inline trailing comments such as
`<shockwaveRadius>5</shockwaveRadius> <!-- radius in cells -->` — is preserved byte for byte.

The test suite asserts this directly: after changing one value, the file must differ from the original by
exactly that one substring.

### Inheritance

`ParentName` chains are resolved the way the game resolves them. Parent children are merged in first, so
scalar lookups naturally land on the child's override while accumulating containers (`comps`,
`modExtensions`, `statBases`) keep every inherited entry. A turret that inherits `MaxHitPoints` from
`AMCTurretBase` therefore reads the inherited value, and editing it writes a local override into the
child def.

### One field map, three consumers

`src/xml/fieldMap.js` is the single source of truth binding each field in the normalised turret object to
its XML location. Extraction, diffing and injection all walk the same table, so a value can never be read
from one tag and written to another. `GET /api/schema` publishes the whole catalogue at runtime.

Each entry carries a `doc` of `building` or `weapon`, which decides both the def the value is read from
and the def an edit is written back to. Component toggles carry it too, so a weapon-side comp such as
`CompProperties_FireModes` is added to the gun def and its file rather than the building's.

## REST API

Every endpoint is JSON-only and deterministic, so scripts and agents can drive the entire pipeline without
a browser. `GET /api/openapi.json` returns a full OpenAPI 3.1 description.

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/extract` | Scan the XML and repopulate SQLite |
| `GET` | `/api/turrets` | List turrets (`?category=`, `?search=`, `?modified=true`, `?warnings=true`) |
| `GET` | `/api/turrets/:defName` | One turret record |
| `PUT` | `/api/turrets/:defName` | Patch a record (nested objects deep-merge, arrays replace) |
| `POST` | `/api/turrets/:defName/revert` | Discard edits for one turret |
| `POST` | `/api/revert-all` | Discard all pending edits |
| `POST` | `/api/restore` | Replace live state from a snapshot |
| `GET` | `/api/diffs` | Unified JSON diff, database vs disk |
| `GET` | `/api/diffs/:defName/xml` | Side-by-side original and pending XML |
| `POST` | `/api/inject` | Write changes to disk (`{dryRun, defNames}`) |
| `POST` | `/api/rollback` | Restore every `.xml.bak` (`{dryRun, keepBackups}`) |
| `GET` | `/api/backups` | List backup files currently on disk |
| `GET` | `/api/metrics` | Dashboard counters |
| `GET` | `/api/audit` | Validation report |
| `GET` | `/api/export` | Download the matrix as CSV or an Excel workbook |
| `GET` | `/api/export/columns` | Column catalogue behind the export dialog |
| `GET` | `/api/schema` | Editable field catalogue with XML paths |

Example round trip:

```bash
curl -X POST http://localhost:3000/api/extract
curl -X PUT  http://localhost:3000/api/turrets/Turret_127mmMark16_Base \
     -H 'Content-Type: application/json' -d '{"stats":{"maxHitPoints":2500}}'
curl       http://localhost:3000/api/diffs
curl -X POST http://localhost:3000/api/inject   -H 'Content-Type: application/json' -d '{}'
curl -X POST http://localhost:3000/api/rollback -H 'Content-Type: application/json' -d '{}'
```

## MCP server

The same pipeline is exposed to AI clients over MCP (stdio), so Claude Desktop, Claude Code, Cursor,
Antigravity and similar tools can read stats, balance in bulk, review diffs and commit them to XML.

```bash
npm run mcp
```

Copy the `amc-turret-editor` entry from `mcp-config-sample.json` into your client's MCP config. The mod
root is resolved from the script's own location, so no working directory is needed; point `AMC_MOD_ROOT`
and `AMC_DB_PATH` at a copy of the mod if you want the AI to experiment without touching your files.

| Tool | Purpose |
| --- | --- |
| `list_turrets` | Compact listing (`category`, `search`, `modified`, `warnings`) |
| `get_turret` | One full record |
| `get_field_catalogue` | Every editable key with type and gating comp |
| `get_metrics` / `get_audit` | Dashboard counters, validation findings |
| `update_turret` | Set fields on one turret by dotted key |
| `batch_update_turrets` | `set` / `mul` / `add` across a filter, in one transaction |
| `revert_turret` / `revert_all` | Discard pending edits |
| `get_diffs` | Pending changes as `key`, `from`, `to` |
| `preview_turret_xml` | Side-by-side XML for one def |
| `inject_xml_changes` | Write to disk — **previews unless `dryRun: false`** |
| `list_backups` / `rollback_xml` | Inspect and restore `.xml.bak` files |
| `extract_defs` | Re-read the XML — refuses while edits are pending |
| `list_ammo` / `get_ammo` / `revert_ammo` | Ammo defs, read and revert |

### Three things the tools do differently to the REST API

**Field keys are validated.** `update_turret` takes flat dotted keys — `stats.maxHitPoints`,
`ballistics.cooldown`, `ammo.magazineSize` — checked against `fieldMap.js` and translated into the nested
patch the services expect. `PUT /api/turrets/:defName` merges whatever it is handed, so a mistaken key
would be stored verbatim, mark the record modified, and then inject nothing; over MCP it comes back as a
`rejected` entry instead. Call `get_field_catalogue` (also served as the `amc://fields` resource) first —
it returns three groups: `fields` (dotted scalar keys), `toggles` (add or remove a whole comp or
modExtension) and `special` (structures with their own injector, currently the `costs` list, which takes
a whole `[{thingDef, count}]` array and accepts `mul`/`add` to scale every count). Fields behind a
switched-off comp are rejected the same way, unless the same call switches the comp on.

**Listings are projections.** The full records are ~365 KB of JSON for the whole mod, which would swamp a
model's context on one call. Listings read the indexed columns instead — about 14 KB for all 65 turrets,
26x smaller than the records and 34x smaller than the raw XML — and full detail stays behind `get_turret`.

**Destructive calls are opt-in.** `inject_xml_changes` and `rollback_xml` dry-run unless told otherwise,
and `extract_defs` refuses to wipe the tables while any edit is still pending, since re-extraction
discards anything that has not been injected. Pass `force: true` to override.

## Spreadsheet export

Export State was replaced by **Export Matrix**, which writes CSV or a real `.xlsx` workbook with
human-readable column headers (`Max Hit Points`, `Turret Cooldown (s)`, `Heat Threshold (shots)`) rather
than raw JSON keys. Numeric columns are written as numbers, so they sort and total correctly.

```bash
# Full 98-column matrix
curl -o matrix.csv 'http://localhost:3000/api/export?format=csv&dataset=turrets'

# Narrower 30-column summary
curl -o summary.csv 'http://localhost:3000/api/export?format=csv&dataset=summary'

# One row per costList entry, and one row per audit finding
curl -o costs.csv 'http://localhost:3000/api/export?format=csv&dataset=costs'
curl -o audit.csv 'http://localhost:3000/api/export?format=csv&dataset=audit'

# All three tables as sheets in one Excel workbook
curl -o matrix.xlsx 'http://localhost:3000/api/export?format=xlsx'

# Only the turrets you have edited
curl -o pending.csv 'http://localhost:3000/api/export?format=csv&scope=modified'
```

| Dataset | Grain | Columns |
| --- | --- | --- |
| `turrets` | One row per turret | 98 |
| `summary` | One row per turret | 30 |
| `costs` | One row per `costList` entry | 6 |
| `audit` | One row per validation finding | 7 |

CSV is UTF-8 with a BOM so Excel reads def labels correctly. The `.xlsx` writer
([`src/xlsx.js`](src/xlsx.js)) builds the ZIP container and Office Open XML parts directly — no
third-party dependency — and produces frozen header rows, auto-filters and sized columns.

`GET /api/export/columns?dataset=turrets` returns the header, type and group of every column, which is
what the export dialog renders as its preview.

## Backups and rollback

The first injection of a file copies it to `<file>.xml.bak`; later injections leave that backup alone, so
it always holds the pristine pre-injection content.

`POST /api/rollback` restores **every** `.xml.bak` it finds under `Common/Defs`, including backups left by
other tooling. Preview first with `GET /api/backups` or `POST /api/rollback -d '{"dryRun":true}'`; the
dashboard shows the on-disk backup count under the metrics panel.

## Database

SQLite at `DevTools/turret_editor.db`.

| Table | Contents |
| --- | --- |
| `files` | Scanned file paths and checksums |
| `turrets` | Building `ThingDef` stats, comp flags, plus `data_json` (live) and `original_json` (pristine) |
| `costs` | `costList` rows |
| `weapons` | Linked weapon `ThingDef` ballistics and ammo stats |
| `mod_extensions` | Barrel, smoker, accuracy and mode-swap parameters |
| `meta` | Extraction timestamps and the known-def index |

The driver is Node's built-in `node:sqlite`. If `better-sqlite3` installs successfully on your platform it
is used instead — `src/sqlite.js` exposes one API for both, and `GET /api/health` reports which is active.

## Configuration

| Variable | Default | Purpose |
| --- | --- | --- |
| `PORT` | `3000` | First port to try |
| `HOST` | `127.0.0.1` | Bind address |
| `AMC_MOD_ROOT` | parent of `TurretEditor/` | Mod root to scan |
| `AMC_DB_PATH` | `<mod root>/DevTools/turret_editor.db` | Database location |
| `AMC_NO_OPEN` | unset | Set to `1` to suppress the browser launch |

## Layout

```
TurretEditor/
  server.js              entry point: port check, listen, open browser
  mcp-server.js          MCP entry point: stdio transport, guarded auto-extract
  src/
    app.js               Fastify app factory (also used by the tests)
    paths.js  db.js  sqlite.js  xlsx.js
    xml/
      scanner.js         position-aware XML scanner
      editor.js          surgical in-place tag editing
      defIndex.js        file scan + ParentName inheritance
      extract.js         XML -> normalised turret records + audit rules
      fieldMap.js        field <-> XML tag binding
      tree.js            tree query helpers
    services/            extract, diff, inject, turret, audit, export, persist
    routes/              api.js, openapi.js
    mcp/                 server.js, tools.js, fieldPatch.js, projections.js
  ui/                    React sources (Vite)
  public/                built dashboard, served statically
  test/                  integration suite
```
