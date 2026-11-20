/*
 * Catalog checks and parity cases. tools/CatalogCheck runs this file with Jint, after
 * engine.js, with CATALOG / PREFABS / CATEGORIES parsed from data/*.json.
 */

function makeHelpers(E) {
  const problems = [];
  const base = id => {
    const s = { prefabId: id, values: E.defaultValues(), custom: {} };
    for (const [k, v] of Object.entries(E.findPrefab(id).defaults || {})) {
      if (!E.isValidValue(k, v)) problems.push(`prefab ${id}: invalid default ${k}=${v}`);
      else s.values[k] = v;
    }
    return s;
  };
  const clone = s => JSON.parse(JSON.stringify(s));
  const mentions = (text, name) =>
    [...String(text).matchAll(/\{\{(?:list:)?([\w,.]+)\}\}/g)].some(m => m[1].split(",").includes(name));
  // an option can name a variable in its text or in an extra field ("socks": "{{legwear}}")
  const optionMentions = (o, name) => Object.values(o).some(v => typeof v === "string" && mentions(v, name));
  // a selection that reaches the variable: some fields only appear under another choice
  // (piece fields under a "build" outfit, the shoes color under a shoe, a style's detail)
  const contextFor = (name, seen = new Set()) => {
    const start = base(PREFABS[0].id);
    if (E.build(start).used.has(name)) return start;
    for (const p of PREFABS) {
      if (!mentions(p.pose, name)) continue;
      const s = base(p.id);
      if (E.build(s).used.has(name)) return s;
    }
    if (seen.has(name)) return null;
    seen.add(name);
    for (const [parent, pdef] of Object.entries(E.VARS)) {
      for (const o of pdef.options) {
        if (!optionMentions(o, name)) continue;
        const c = contextFor(parent, seen);
        if (!c) continue;
        const s = clone(c); s.values[parent] = o.id;
        if (E.build(s).used.has(name)) return s;
      }
    }
    return null;
  };
  return { problems, base, clone, mentions, contextFor };
}

function runChecks() {
  const E = PromptEngine.create(CATALOG, PREFABS);
  const { problems, base, clone, contextFor } = makeHelpers(E);
  const out = { prefabs: 0, lengths: [] };
  const glitch = text => text.split("\n").some(l => /\s[.,]|\.\.|,,|, ,| {2}|\b(?:and|with)[.,]|/.test(l));
  const ids = new Set();
  for (const p of PREFABS) {
    if (ids.has(p.id)) problems.push("duplicate prefab id " + p.id);
    ids.add(p.id);
    if (!CATEGORIES.includes(p.category)) problems.push(`prefab ${p.id}: unknown category ${p.category}`);
    const r = E.build(base(p.id));
    if (/\{\{/.test(r.text)) problems.push(`prefab ${p.id}: unresolved placeholder in prompt`);
    if (glitch(r.text)) problems.push(`prefab ${p.id}: spacing/punctuation glitch`);
    out.lengths.push(r.text.length);
    out.prefabs++;
  }
  // {{var.field}} and field list items: every option of var must carry the field
  const texts = [...Object.values(CATALOG.blocks), ...PREFABS.map(p => p.pose),
    ...Object.values(E.VARS).flatMap(d => d.options.map(o => o.en))];
  const fieldRefs = t => [
    ...[...String(t).matchAll(/\{\{(\w+)\.(\w+)\}\}/g)].map(m => [m[1], m[2]]),
    ...[...String(t).matchAll(/\{\{list:([\w,.]+)\}\}/g)].flatMap(m => m[1].split(",").filter(i => i.includes(".")).map(i => i.split(".")))];
  for (const t of texts) {
    for (const [v, f] of fieldRefs(t)) {
      const def = E.VARS[v];
      if (!def) { problems.push(`{{${v}.${f}}}: unknown variable`); continue; }
      for (const o of def.options) if (!(f in o)) problems.push(`var ${v}=${o.id}: missing field "${f}"`);
    }
  }
  for (const [name, def] of Object.entries(E.VARS)) {
    const ctx = contextFor(name);
    if (!ctx) problems.push(`var ${name}: no selection reaches it, so it never shows up`);
    const optIds = new Set();
    for (const o of def.options) {
      if (optIds.has(o.id)) problems.push(`var ${name}: duplicate option id ${o.id}`);
      optIds.add(o.id);
      const s = clone(ctx || base(PREFABS[0].id)); s.values[name] = def.multi ? [o.id] : o.id;
      const r = E.build(s);
      if (/\{\{/.test(r.text)) problems.push(`var ${name}=${o.id}: unresolved placeholder`);
      if (glitch(r.text)) problems.push(`var ${name}=${o.id}: spacing/punctuation glitch`);
      for (const m of o.en.matchAll(/\{\{list:([\w,.]+)\}\}/g)) {
        for (const item of m[1].split(",")) {
          const [v, f] = item.split(".");
          if (!E.VARS[v]) { problems.push(`var ${name}=${o.id}: list names unknown variable ${item}`); continue; }
          // the text that lands in the list: the option itself, or its field and what the field names
          const pieces = f
            ? E.VARS[v].options.flatMap(io => [[`${v}=${io.id}.${f}`, String(io[f] || "")],
                ...[...String(io[f] || "").matchAll(/\{\{(\w+)\}\}/g)].flatMap(mm => (E.VARS[mm[1]] || { options: [] }).options.map(x => [`${mm[1]}=${x.id}`, x.en]))])
            : E.VARS[v].options.map(io => [`${v}=${io.id}`, io.en]);
          for (const [where, text] of pieces) if (text.includes(",")) problems.push(`${where}: comma inside a list item`);
        }
      }
    }
    const defaults = def.multi ? (Array.isArray(def.default) ? def.default : [null]) : [def.default];
    for (const d of defaults) if (!def.options.some(o => o.id === d)) problems.push(`var ${name}: default ${d} is not an option`);
    if (def.multi) {
      // all options together (one per slot) must read cleanly
      const s = clone(ctx || base(PREFABS[0].id));
      const seen = new Set();
      s.values[name] = def.options.filter(o => !o.slot || (!seen.has(o.slot) && seen.add(o.slot))).map(o => o.id);
      if (glitch(E.build(s).text) || /\{\{/.test(E.build(s).text)) problems.push(`var ${name}: glitch with several options at once`);
    }
  }
  out.problems = problems;
  return JSON.stringify(out);
}

// Selections and edit pairs for comparing engine.js with the C# engine: every prefab, every
// option of every variable where it shows up, and seeded random combinations.
function parityCases() {
  const E = PromptEngine.create(CATALOG, PREFABS);
  const { base, clone, contextFor } = makeHelpers(E);
  let seed = 20261119;
  const rnd = () => { // mulberry32
    seed = (seed + 0x6D2B79F5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  const pick = list => list[Math.floor(rnd() * list.length)];
  const randomValue = (name, s) => {
    const def = E.VARS[name];
    if (def.multi) {
      const out = [], slots = new Set();
      for (const o of def.options) {
        if (rnd() < 0.7 || (o.slot && slots.has(o.slot))) continue;
        out.push(o.id); if (o.slot) slots.add(o.slot);
      }
      return out;
    }
    if (rnd() < 0.03) { s.custom[name] = "a custom " + name + ", typed by hand"; return E.CUSTOM; }
    return pick(def.options).id;
  };
  const randomize = (s, count) => {
    const names = [...E.build(s).used];
    for (let i = 0; i < count; i++) { const n = pick(names); s.values[n] = randomValue(n, s); }
    return s;
  };

  const cases = [], edits = [];
  for (const p of PREFABS) cases.push(["prefab " + p.id, base(p.id)]);
  for (const [name, def] of Object.entries(E.VARS)) {
    const ctx = contextFor(name);
    if (!ctx) continue;
    for (const o of def.options) {
      const s = clone(ctx); s.values[name] = def.multi ? [o.id] : o.id;
      cases.push([`${name}=${o.id}`, s]);
      edits.push([`edit ${name}=${o.id}`, ctx, s]);
    }
  }
  for (let i = 0; i < 500; i++) {
    const s = randomize(base(pick(PREFABS).id), 30);
    cases.push(["random " + i, s]);
    const t = randomize(clone(s), 1 + Math.floor(rnd() * 3));
    if (rnd() < 0.2) t.prefabId = pick(PREFABS).id;
    edits.push(["random edit " + i, s, t]);
  }
  return JSON.stringify({
    cases: cases.map(([label, sel]) => { const r = E.build(sel); return { label, sel, text: r.text, used: [...r.used] }; }),
    edits: edits.map(([label, a, b]) => ({ label, a, b, text: E.editRequest(a, b).text }))
  });
}
