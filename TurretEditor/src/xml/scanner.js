/**
 * Position-aware XML scanner.
 *
 * RimWorld Defs are hand-authored: they carry comments, mixed tabs/spaces and
 * meaningful formatting that a parse -> serialise round trip would destroy.
 * Every node this scanner produces therefore keeps the exact character offsets
 * it occupies in the source, so injection can rewrite one tag's inner text and
 * leave every other byte on disk untouched.
 */

const NAME_RE = /[A-Za-z_:][-A-Za-z0-9_:.]*/y;

/** Skip comments / CDATA / processing instructions / doctype starting at `i`. */
function skipNonElement(src, i) {
  if (src.startsWith('<!--', i)) {
    const end = src.indexOf('-->', i + 4);
    return end === -1 ? src.length : end + 3;
  }
  if (src.startsWith('<![CDATA[', i)) {
    const end = src.indexOf(']]>', i + 9);
    return end === -1 ? src.length : end + 3;
  }
  if (src.startsWith('<?', i)) {
    const end = src.indexOf('?>', i + 2);
    return end === -1 ? src.length : end + 2;
  }
  if (src.startsWith('<!', i)) {
    const end = src.indexOf('>', i + 2);
    return end === -1 ? src.length : end + 1;
  }
  return -1;
}

/** Find the index just past the `>` of the tag opening at `i`, respecting quotes. */
function findTagEnd(src, i) {
  let quote = null;
  for (let p = i; p < src.length; p++) {
    const c = src[p];
    if (quote) {
      if (c === quote) quote = null;
    } else if (c === '"' || c === "'") {
      quote = c;
    } else if (c === '>') {
      return p + 1;
    }
  }
  return -1;
}

function parseAttrs(raw) {
  const attrs = {};
  const re = /([A-Za-z_:][-A-Za-z0-9_:.]*)\s*=\s*("([^"]*)"|'([^']*)')/g;
  let m;
  while ((m = re.exec(raw)) !== null) attrs[m[1]] = m[3] !== undefined ? m[3] : m[4];
  return attrs;
}

/**
 * Locate the matching close tag for `name` opened at `openEnd`, honouring
 * nesting of same-named elements.
 * @returns {{closeStart:number, closeEnd:number}|null}
 */
function findMatchingClose(src, name, openEnd, limit) {
  let depth = 1;
  let p = openEnd;
  while (p < limit) {
    const lt = src.indexOf('<', p);
    if (lt === -1 || lt >= limit) return null;
    const skipped = skipNonElement(src, lt);
    if (skipped !== -1) { p = skipped; continue; }

    if (src[lt + 1] === '/') {
      NAME_RE.lastIndex = lt + 2;
      const m = NAME_RE.exec(src);
      const tagEnd = findTagEnd(src, lt);
      if (m && m[0] === name) {
        depth--;
        if (depth === 0) return { closeStart: lt, closeEnd: tagEnd };
      }
      p = tagEnd === -1 ? limit : tagEnd;
      continue;
    }

    NAME_RE.lastIndex = lt + 1;
    const m = NAME_RE.exec(src);
    const tagEnd = findTagEnd(src, lt);
    if (tagEnd === -1) return null;
    if (m && m[0] === name && src[tagEnd - 2] !== '/') depth++;
    p = tagEnd;
  }
  return null;
}

/**
 * Enumerate the direct element children inside `[from, to)`.
 * @returns {Array<{tag:string, attrs:object, outerStart:number, outerEnd:number,
 *                 innerStart:number, innerEnd:number, selfClosing:boolean}>}
 */
export function scanChildren(src, from, to) {
  const out = [];
  let p = from;
  while (p < to) {
    const lt = src.indexOf('<', p);
    if (lt === -1 || lt >= to) break;

    const skipped = skipNonElement(src, lt);
    if (skipped !== -1) { p = skipped; continue; }
    if (src[lt + 1] === '/') break; // closing tag of the parent

    NAME_RE.lastIndex = lt + 1;
    const m = NAME_RE.exec(src);
    if (!m) { p = lt + 1; continue; }

    const tag = m[0];
    const openEnd = findTagEnd(src, lt);
    if (openEnd === -1) break;

    const attrs = parseAttrs(src.slice(lt + 1 + tag.length, openEnd - 1));
    const selfClosing = src[openEnd - 2] === '/';

    if (selfClosing) {
      out.push({ tag, attrs, outerStart: lt, outerEnd: openEnd, innerStart: openEnd, innerEnd: openEnd, selfClosing: true });
      p = openEnd;
      continue;
    }

    const close = findMatchingClose(src, tag, openEnd, to);
    if (!close) { p = openEnd; continue; }

    out.push({
      tag, attrs,
      outerStart: lt, outerEnd: close.closeEnd,
      innerStart: openEnd, innerEnd: close.closeStart,
      selfClosing: false,
    });
    p = close.closeEnd;
  }
  return out;
}

/** Decode the five predefined XML entities plus numeric references. */
export function decodeEntities(s) {
  return s.replace(/&(#x?[0-9A-Fa-f]+|amp|lt|gt|quot|apos);/g, (whole, body) => {
    switch (body) {
      case 'amp': return '&';
      case 'lt': return '<';
      case 'gt': return '>';
      case 'quot': return '"';
      case 'apos': return "'";
      default:
        return body[1] === 'x' || body[1] === 'X'
          ? String.fromCodePoint(parseInt(body.slice(2), 16))
          : String.fromCodePoint(parseInt(body.slice(1), 10));
    }
  });
}

export function encodeEntities(s) {
  return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

/**
 * Text content of a node, with comments stripped. Only meaningful for leaf
 * nodes; container nodes return their flattened text which callers ignore.
 */
export function nodeText(src, node) {
  let raw = src.slice(node.innerStart, node.innerEnd);
  raw = raw.replace(/<!--[\s\S]*?-->/g, '');
  return decodeEntities(raw).trim();
}

/** True when the node has element children (i.e. it is a container, not a leaf). */
export function isContainer(src, node) {
  return scanChildren(src, node.innerStart, node.innerEnd).length > 0;
}

/**
 * Build a detached, position-free tree for a node — used by extraction, where
 * inherited defs get merged and offsets stop being meaningful.
 * @returns {{tag:string, attrs:object, children:Array, text:string}}
 */
export function toTree(src, node) {
  const kids = scanChildren(src, node.innerStart, node.innerEnd);
  return {
    tag: node.tag,
    attrs: node.attrs,
    children: kids.map((k) => toTree(src, k)),
    text: kids.length ? '' : nodeText(src, node),
  };
}

/** Parse a whole document and return the top-level `<Defs>` children as trees. */
export function parseDefs(src) {
  const roots = scanChildren(src, 0, src.length);
  const defsRoot = roots.find((r) => r.tag === 'Defs');
  if (!defsRoot) return [];
  return scanChildren(src, defsRoot.innerStart, defsRoot.innerEnd).map((n) => toTree(src, n));
}
