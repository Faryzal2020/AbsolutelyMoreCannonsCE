/**
 * MCP server wiring.
 *
 * The low-level `Server` is used rather than the schema-driven helper so the
 * tool schemas stay hand-written JSON Schema, matching routes/openapi.js, and
 * so failures come back as a structured `{ ok: false, error }` payload the model
 * can act on instead of a transport fault.
 *
 * Kept separate from the mcp-server.js entry point because the test suite has to
 * set AMC_MOD_ROOT before any module that reads paths.js is imported.
 */

import fs from 'node:fs';
import path from 'node:path';
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import {
  CallToolRequestSchema,
  ListResourcesRequestSchema,
  ListToolsRequestSchema,
  ReadResourceRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';
import { APP_ROOT } from '../paths.js';
import { TOOLS, TOOL_BY_NAME } from './tools.js';
import { fieldCatalogue } from './fieldPatch.js';

const { version } = JSON.parse(fs.readFileSync(path.join(APP_ROOT, 'package.json'), 'utf8'));

const FIELDS_URI = 'amc://fields';

/** Minimal check of the declared schema: required keys and top-level types. */
function validate(schema, args) {
  for (const key of schema.required || []) {
    if (args[key] === undefined) return `Missing required argument: ${key}`;
  }
  for (const [key, value] of Object.entries(args)) {
    const spec = schema.properties?.[key];
    if (!spec || value === undefined || value === null) continue;
    const actual = Array.isArray(value) ? 'array' : typeof value;
    const expected = spec.type;
    if (expected && expected !== actual && !(expected === 'number' && actual === 'number')) {
      return `Argument "${key}" must be ${expected}, received ${actual}.`;
    }
    if (spec.enum && !spec.enum.includes(value)) {
      return `Argument "${key}" must be one of: ${spec.enum.join(', ')}.`;
    }
  }
  return null;
}

const asResult = (payload, isError = false) => ({
  content: [{ type: 'text', text: JSON.stringify(payload, null, 2) }],
  isError,
});

/**
 * Build the MCP server over an already-open database handle.
 * @param {{db: object}} deps
 */
export function createMcpServer({ db }) {
  const server = new Server(
    { name: 'amc-turret-editor', version },
    { capabilities: { tools: {}, resources: {} } },
  );

  server.setRequestHandler(ListToolsRequestSchema, async () => ({
    tools: TOOLS.map(({ name, description, inputSchema }) => ({ name, description, inputSchema })),
  }));

  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const tool = TOOL_BY_NAME.get(request.params.name);
    if (!tool) return asResult({ ok: false, error: `Unknown tool: ${request.params.name}` }, true);

    const args = request.params.arguments ?? {};
    const invalid = validate(tool.inputSchema, args);
    if (invalid) return asResult({ ok: false, error: invalid }, true);

    try {
      const payload = tool.handler(db, args);
      return asResult(payload, payload?.ok === false);
    } catch (err) {
      return asResult({ ok: false, error: err.message }, true);
    }
  });

  // The field catalogue is also a resource so clients that prefetch resources
  // learn the valid edit keys without spending a tool call.
  server.setRequestHandler(ListResourcesRequestSchema, async () => ({
    resources: [{
      uri: FIELDS_URI,
      name: 'Editable field catalogue',
      description: 'Every dotted field key accepted by update_turret, with type and gating comp.',
      mimeType: 'application/json',
    }],
  }));

  server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
    if (request.params.uri !== FIELDS_URI) {
      throw new Error(`Unknown resource: ${request.params.uri}`);
    }
    return {
      contents: [{
        uri: FIELDS_URI,
        mimeType: 'application/json',
        text: JSON.stringify(fieldCatalogue(), null, 2),
      }],
    };
  });

  return server;
}
