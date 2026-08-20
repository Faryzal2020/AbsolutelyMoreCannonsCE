/** Small query helpers over the detached trees produced by scanner.toTree(). */

export function child(node, tag) {
  if (!node) return null;
  return node.children.find((c) => c.tag === tag) || null;
}

/** Last matching child wins — mirrors RimWorld's "child def overrides parent". */
export function lastChild(node, tag) {
  if (!node) return null;
  for (let i = node.children.length - 1; i >= 0; i--) {
    if (node.children[i].tag === tag) return node.children[i];
  }
  return null;
}

export function text(node, tag, fallback = '') {
  const c = lastChild(node, tag);
  return c && c.text ? c.text : fallback;
}

/** Follow a slash-separated path of tags, taking the last match at each level. */
export function textPath(node, path, fallback = '') {
  let cur = node;
  const parts = path.split('/').filter(Boolean);
  for (const p of parts) {
    cur = lastChild(cur, p);
    if (!cur) return fallback;
  }
  return cur.text || fallback;
}

/** Every `<li>` across every container named `tag` (containers accumulate). */
export function allListItems(node, tag) {
  const out = [];
  for (const c of node.children) {
    if (c.tag === tag) out.push(...c.children.filter((li) => li.tag === 'li'));
  }
  return out;
}

/** Find a `<li Class="...">` whose Class attribute ends with / contains `suffix`. */
export function findByClass(items, suffix) {
  return items.find((li) => (li.attrs.Class || '').includes(suffix)) || null;
}

export function bool(node, tag, fallback = false) {
  const v = text(node, tag, null);
  if (v === null) return fallback;
  return v.toLowerCase() === 'true';
}

export function num(node, tag, fallback = 0) {
  const v = text(node, tag, null);
  if (v === null || v === '') return fallback;
  const n = Number(v);
  return Number.isNaN(n) ? fallback : n;
}

/** `<statBases><X>..</X></statBases>` lookup across all statBases blocks. */
export function stat(node, name, fallback = null) {
  for (let i = node.children.length - 1; i >= 0; i--) {
    const c = node.children[i];
    if (c.tag !== 'statBases') continue;
    const s = lastChild(c, name);
    if (s && s.text !== '') return s.text;
  }
  return fallback;
}

export function statNum(node, name, fallback = 0) {
  const v = stat(node, name, null);
  if (v === null) return fallback;
  const n = Number(v);
  return Number.isNaN(n) ? fallback : n;
}

/** Collapse repeated `<li>` text into a comma list, e.g. selectableBurstCounts. */
export function liTexts(node) {
  if (!node) return [];
  return node.children.filter((c) => c.tag === 'li').map((c) => c.text).filter((t) => t !== '');
}
