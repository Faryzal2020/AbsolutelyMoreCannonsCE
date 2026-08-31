/**
 * Surgical, byte-preserving XML editing.
 *
 * Every operation rewrites the smallest possible span of the source file, so
 * comments, tabs-vs-spaces, blank lines and tag ordering elsewhere in the file
 * survive injection completely unchanged.
 */

import { scanChildren, encodeEntities, nodeText } from './scanner.js';

/** Locate the `<ThingDef>` (or any Def) block that declares `<defName>`. */
export function findDefBlock(src, defName) {
  const roots = scanChildren(src, 0, src.length);
  const defsRoot = roots.find((r) => r.tag === 'Defs');
  if (!defsRoot) return null;
  for (const def of scanChildren(src, defsRoot.innerStart, defsRoot.innerEnd)) {
    const kids = scanChildren(src, def.innerStart, def.innerEnd);
    const dn = kids.find((k) => k.tag === 'defName');
    if (dn && nodeText(src, dn) === defName) return def;
  }
  return null;
}

function stepMatches(src, node, step) {
  if (node.tag !== step.tag) return false;
  if (step.cls && !(node.attrs.Class || '').includes(step.cls)) return false;
  if (step.match) {
    const kids = scanChildren(src, node.innerStart, node.innerEnd);
    const target = kids.find((k) => k.tag === step.match.tag);
    if (!target) return false;
    if (step.match.contains && !nodeText(src, target).includes(step.match.contains)) return false;
  }
  return true;
}

/** Last matching direct child — the child def's own override wins. */
export function findChild(src, parent, step) {
  const kids = scanChildren(src, parent.innerStart, parent.innerEnd);
  for (let i = kids.length - 1; i >= 0; i--) {
    if (stepMatches(src, kids[i], step)) return kids[i];
  }
  return null;
}

/**
 * Walk a field path from a def block.
 * @returns {{node, chain:Array, missingIndex:number}} — missingIndex is -1 when
 *          the full path resolved, otherwise the first step that was absent.
 */
export function resolvePath(src, defNode, path) {
  let cur = defNode;
  const chain = [defNode];
  for (let i = 0; i < path.length; i++) {
    const next = findChild(src, cur, path[i]);
    if (!next) return { node: null, chain, missingIndex: i };
    cur = next;
    chain.push(next);
  }
  return { node: cur, chain, missingIndex: -1 };
}

// --- formatting helpers ----------------------------------------------------

function lineIndent(src, pos) {
  const lineStart = src.lastIndexOf('\n', pos - 1) + 1;
  const m = /^[ \t]*/.exec(src.slice(lineStart, pos));
  return m ? m[0] : '';
}

/** Guess the file's indent unit so inserted markup matches its neighbours. */
function indentUnit(src) {
  const m = /\n([ \t]+)</.exec(src);
  if (!m) return '  ';
  return m[1].includes('\t') ? '\t' : ' '.repeat(Math.min(m[1].length, 4) || 2);
}

/** Re-indent a template body (written with 2-space steps) to the target style. */
function indentBlock(body, baseIndent, unit) {
  return body
    .split('\n')
    .map((line) => {
      if (!line.trim()) return '';
      const depth = Math.floor((/^ */.exec(line)[0].length) / 2);
      return baseIndent + unit.repeat(depth) + line.trim();
    })
    .join('\n');
}

/**
 * Insert `body` as a new last child of `parent`, matching sibling indentation.
 * Expands a self-closing parent into an open/close pair when required.
 */
function insertChild(src, parent, body) {
  const unit = indentUnit(src);
  const parentIndent = lineIndent(src, parent.outerStart);
  const kids = scanChildren(src, parent.innerStart, parent.innerEnd);
  const childIndent = kids.length ? lineIndent(src, kids[kids.length - 1].outerStart) : parentIndent + unit;

  if (parent.selfClosing) {
    const open = src.slice(parent.outerStart, parent.outerEnd).replace(/\s*\/>$/, '>');
    const block = indentBlock(body, parentIndent + unit, unit);
    const replacement = `${open}\n${block}\n${parentIndent}</${parent.tag}>`;
    return src.slice(0, parent.outerStart) + replacement + src.slice(parent.outerEnd);
  }

  const closeLineStart = src.lastIndexOf('\n', parent.innerEnd - 1) + 1;
  const tailIsBlank = /^[ \t]*$/.test(src.slice(closeLineStart, parent.innerEnd));
  const block = indentBlock(body, childIndent, unit);

  if (tailIsBlank && closeLineStart > parent.innerStart) {
    return src.slice(0, closeLineStart) + block + '\n' + src.slice(closeLineStart);
  }
  // Single-line container: expand it onto its own lines.
  const inner = src.slice(parent.innerStart, parent.innerEnd).trim();
  const rebuilt = `\n${inner ? childIndent + inner + '\n' : ''}${block}\n${parentIndent}`;
  return src.slice(0, parent.innerStart) + rebuilt + src.slice(parent.innerEnd);
}

/** Remove a node together with the whitespace-only remainder of its line. */
function removeNode(src, node) {
  let start = node.outerStart;
  let end = node.outerEnd;
  const lineStart = src.lastIndexOf('\n', start - 1) + 1;
  if (/^[ \t]*$/.test(src.slice(lineStart, start))) start = lineStart;
  const nl = src.indexOf('\n', end);
  if (nl !== -1 && /^[ \t]*$/.test(src.slice(end, nl))) end = nl + 1;
  return src.slice(0, start) + src.slice(end);
}

/** Replace only the inner text of a leaf node, keeping its tags and attributes. */
function replaceInner(src, node, text) {
  if (node.selfClosing) {
    const open = src.slice(node.outerStart, node.outerEnd).replace(/\s*\/>$/, '>');
    return src.slice(0, node.outerStart) + `${open}${text}</${node.tag}>` + src.slice(node.outerEnd);
  }
  // Preserve any trailing comment that sits inside the tag (common in this mod).
  const inner = src.slice(node.innerStart, node.innerEnd);
  const comment = /(\s*<!--[\s\S]*?-->\s*)$/.exec(inner);
  const suffix = comment ? comment[1] : '';
  return src.slice(0, node.innerStart) + text + suffix + src.slice(node.innerEnd);
}

// --- public operations -----------------------------------------------------

export const SKIP_COMPONENT_MISSING = 'component-not-declared';
export const SKIP_DEF_MISSING = 'def-not-found';
export const SKIP_NO_CHANGE = 'no-change';

/**
 * Set a scalar leaf value at `path` inside the def `defName`.
 *
 * Plain container tags (statBases, building, recoilAnimation, ...) are created
 * on demand, because writing them into a child def is a legitimate RimWorld
 * override. A missing `<li Class="...">` is *not* invented: comp and
 * modExtension lists are additive, so synthesising one would duplicate an
 * inherited component instead of overriding it. Those edits are reported as
 * skipped so the caller can surface them.
 */
export function setValue(src, defName, path, text) {
  const defNode = findDefBlock(src, defName);
  if (!defNode) return { content: src, changed: false, reason: SKIP_DEF_MISSING };

  const { node, chain, missingIndex } = resolvePath(src, defNode, path);

  if (node) {
    if (nodeText(src, node) === text) return { content: src, changed: false, reason: SKIP_NO_CHANGE };
    return { content: replaceInner(src, node, encodeEntities(text)), changed: true };
  }

  if (path.slice(missingIndex).some((s) => s.cls || s.match)) {
    return { content: src, changed: false, reason: SKIP_COMPONENT_MISSING };
  }

  // Build the missing tail (e.g. <statBases><MaxHitPoints>500</MaxHitPoints></statBases>).
  const tail = path.slice(missingIndex);
  let body = `<${tail[tail.length - 1].tag}>${encodeEntities(text)}</${tail[tail.length - 1].tag}>`;
  for (let i = tail.length - 2; i >= 0; i--) {
    body = `<${tail[i].tag}>\n${body.split('\n').map((l) => '  ' + l).join('\n')}\n</${tail[i].tag}>`;
  }
  return { content: insertChild(src, chain[chain.length - 1], body), changed: true };
}

/** Replace a `<li>` list such as selectableBurstCounts or maxRPMs. */
export function setListValue(src, defName, path, items) {
  const inner = items.map((v) => `  <li>${encodeEntities(v)}</li>`).join('\n');
  const leaf = path[path.length - 1].tag;
  const defNode = findDefBlock(src, defName);
  if (!defNode) return { content: src, changed: false, reason: SKIP_DEF_MISSING };

  const { node, chain, missingIndex } = resolvePath(src, defNode, path);

  if (node) {
    if (items.length === 0) return { content: removeNode(src, node), changed: true };
    const unit = indentUnit(src);
    const indent = lineIndent(src, node.outerStart);
    const rebuilt = `<${leaf}>\n${indentBlock(inner, indent + unit, unit)}\n${indent}</${leaf}>`;
    if (src.slice(node.outerStart, node.outerEnd) === rebuilt) {
      return { content: src, changed: false, reason: SKIP_NO_CHANGE };
    }
    return { content: src.slice(0, node.outerStart) + rebuilt + src.slice(node.outerEnd), changed: true };
  }

  if (items.length === 0) return { content: src, changed: false, reason: SKIP_NO_CHANGE };
  if (path.slice(missingIndex).some((s) => s.cls || s.match)) {
    return { content: src, changed: false, reason: SKIP_COMPONENT_MISSING };
  }
  return { content: insertChild(src, chain[chain.length - 1], `<${leaf}>\n${inner}\n</${leaf}>`), changed: true };
}

/** Rewrite `<costList>` to exactly the supplied resource rows. */
export function setCostList(src, defName, costs) {
  const defNode = findDefBlock(src, defName);
  if (!defNode) return { content: src, changed: false, reason: SKIP_DEF_MISSING };

  const unit = indentUnit(src);
  const existing = findChild(src, defNode, { tag: 'costList' });
  const rows = costs
    .filter((c) => c.thingDef && String(c.thingDef).trim())
    .map((c) => `  <${c.thingDef.trim()}>${parseInt(c.count, 10) || 0}</${c.thingDef.trim()}>`)
    .join('\n');

  if (existing) {
    if (!rows) return { content: removeNode(src, existing), changed: true };
    const indent = lineIndent(src, existing.outerStart);
    const rebuilt = `<costList>\n${indentBlock(rows, indent + unit, unit)}\n${indent}</costList>`;
    if (src.slice(existing.outerStart, existing.outerEnd) === rebuilt) {
      return { content: src, changed: false, reason: SKIP_NO_CHANGE };
    }
    return { content: src.slice(0, existing.outerStart) + rebuilt + src.slice(existing.outerEnd), changed: true };
  }

  if (!rows) return { content: src, changed: false, reason: SKIP_NO_CHANGE };
  return { content: insertChild(src, defNode, `<costList>\n${rows}\n</costList>`), changed: true };
}

/** Add a `<li Class="...">` component/extension to its container. */
export function addComponent(src, defName, containerTag, cls, body) {
  const defNode = findDefBlock(src, defName);
  if (!defNode) return { content: src, changed: false, reason: SKIP_DEF_MISSING };

  const container = findChild(src, defNode, { tag: containerTag });
  if (container && findChild(src, container, { tag: 'li', cls })) {
    return { content: src, changed: false, reason: SKIP_NO_CHANGE };
  }
  if (container) return { content: insertChild(src, container, body), changed: true };

  const wrapped = `<${containerTag}>\n${body.split('\n').map((l) => (l.trim() ? '  ' + l : l)).join('\n')}\n</${containerTag}>`;
  return { content: insertChild(src, defNode, wrapped), changed: true };
}

/** Remove a `<li Class="...">` component/extension if the def declares one. */
export function removeComponent(src, defName, containerTag, cls) {
  const defNode = findDefBlock(src, defName);
  if (!defNode) return { content: src, changed: false, reason: SKIP_DEF_MISSING };

  const container = findChild(src, defNode, { tag: containerTag });
  if (!container) return { content: src, changed: false, reason: SKIP_COMPONENT_MISSING };

  const li = findChild(src, container, { tag: 'li', cls });
  if (!li) return { content: src, changed: false, reason: SKIP_COMPONENT_MISSING };

  return { content: removeNode(src, li), changed: true };
}

/** Raw text of a def block — used by the diff viewer. */
export function defBlockText(src, defName) {
  const node = findDefBlock(src, defName);
  return node ? src.slice(node.outerStart, node.outerEnd) : null;
}
