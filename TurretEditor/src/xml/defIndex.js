import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { XMLValidator } from 'fast-xml-parser';
import { parseDefs } from './scanner.js';

/** Recursively collect *.xml (never *.xml.bak) under `dir`. */
export function findXmlFiles(dir) {
  const out = [];
  if (!fs.existsSync(dir)) return out;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...findXmlFiles(full));
    else if (entry.isFile() && entry.name.toLowerCase().endsWith('.xml')) out.push(full);
  }
  return out.sort();
}

export function checksum(content) {
  return crypto.createHash('sha1').update(content, 'utf8').digest('hex');
}

/**
 * Index of every Def in the mod, able to resolve ParentName inheritance the
 * same way RimWorld's def loader does.
 */
export class DefIndex {
  constructor() {
    this.byDefName = new Map();  // defName -> {tree, file}
    this.byName = new Map();     // Name="..." abstract base -> {tree, file}
    this.malformed = [];         // {file, error}
    this.files = new Map();      // absolute path -> {content, checksum}
    this._mergeCache = new Map();
  }

  addFile(absPath, content) {
    const check = XMLValidator.validate(content, { allowBooleanAttributes: true });
    if (check !== true) {
      this.malformed.push({ file: absPath, error: check.err?.msg || 'invalid XML', line: check.err?.line });
      return;
    }
    this.files.set(absPath, { content, checksum: checksum(content) });
    for (const tree of parseDefs(content)) {
      const defNameNode = tree.children.find((c) => c.tag === 'defName');
      const defName = defNameNode?.text;
      const nameAttr = tree.attrs.Name;
      if (defName) this.byDefName.set(defName, { tree, file: absPath });
      if (nameAttr) this.byName.set(nameAttr, { tree, file: absPath });
    }
  }

  scan(rootDir) {
    for (const f of findXmlFiles(rootDir)) {
      try {
        this.addFile(f, fs.readFileSync(f, 'utf8'));
      } catch (err) {
        this.malformed.push({ file: f, error: err.message });
      }
    }
    return this;
  }

  lookup(name) {
    return this.byName.get(name) || this.byDefName.get(name) || null;
  }

  /**
   * Merge a def with its ancestors. Parent children come first so that
   * `lastChild()` lookups naturally resolve to the child def's override, while
   * accumulating containers (comps, modExtensions, statBases, ...) keep every
   * inherited entry — matching how RimWorld composes defs.
   */
  merged(tree) {
    const parentName = tree.attrs.ParentName;
    if (!parentName) return tree;

    if (this._mergeCache.has(tree)) return this._mergeCache.get(tree);

    const parent = this.lookup(parentName);
    if (!parent) return tree;

    const mergedParent = this.merged(parent.tree);
    const result = {
      tag: tree.tag,
      attrs: tree.attrs,
      text: tree.text,
      children: [...mergedParent.children, ...tree.children],
    };
    this._mergeCache.set(tree, result);
    return result;
  }

  /** Full ParentName chain, nearest ancestor first. */
  ancestry(tree) {
    const chain = [];
    let cur = tree;
    const seen = new Set();
    while (cur?.attrs?.ParentName && !seen.has(cur.attrs.ParentName)) {
      seen.add(cur.attrs.ParentName);
      chain.push(cur.attrs.ParentName);
      cur = this.lookup(cur.attrs.ParentName)?.tree;
    }
    return chain;
  }
}
