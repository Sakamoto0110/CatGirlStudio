"""Checks every prefab and every option of every variable.

Runs data/*.js and engine.js in V8 (no browser needed) and reports unresolved
{{placeholders}}, invalid prefab defaults, duplicate ids and spacing glitches.

    pip install mini-racer
    python tools/check_prefabs.py            # from the project folder
    python tools/check_prefabs.py --show     # also print two sample prompts
"""
import sys, json
from pathlib import Path
from py_mini_racer import MiniRacer

root = Path(__file__).resolve().parent.parent
ctx = MiniRacer()
ctx.eval("var window = globalThis;")
for f in ("data/catalog.js", "data/prefabs.js", "engine.js"):
    ctx.eval(root.joinpath(f).read_text(encoding="utf-8"))

report = ctx.eval(r"""
(() => {
  const E = window.PromptEngine.create(window.CATALOG, window.PREFABS);
  const out = { prefabs: [], problems: [], lengths: [] };
  const ids = new Set();
  const glitch = text => text.split("\n").some(l => /\s[.,]|\.\.|,,|, ,| {2}|\b(?:and|with)[.,]|\uE000/.test(l));
  const base = id => {
    const s = { prefabId: id, values: E.defaultValues(), custom: {} };
    for (const [k, v] of Object.entries(E.findPrefab(id).defaults || {})) {
      if (!E.isValidValue(k, v)) out.problems.push(`prefab ${id}: invalid default ${k}=${v}`);
      else s.values[k] = v;
    }
    return s;
  };
  for (const p of window.PREFABS) {
    if (ids.has(p.id)) out.problems.push("duplicate prefab id " + p.id);
    ids.add(p.id);
    if (!window.CATEGORIES.includes(p.category)) out.problems.push(`prefab ${p.id}: unknown category ${p.category}`);
    const r = E.build(base(p.id));
    if (/\{\{/.test(r.text)) out.problems.push(`prefab ${p.id}: unresolved placeholder in prompt`);
    if (glitch(r.text)) out.problems.push(`prefab ${p.id}: spacing/punctuation glitch`);
    out.lengths.push(r.text.length);
    out.prefabs.push([p.id, r.used.size]);
  }
  // every option of every variable must resolve cleanly, tested in a selection that
  // actually reaches the variable: some fields only appear under another choice
  // (the piece fields under a "build" outfit, the shoes color under a shoe)
  const mentions = (text, name) =>
    [...String(text).matchAll(/\{\{(?:list:)?([\w,]+)\}\}/g)].some(m => m[1].split(",").includes(name));
  const contextFor = (name, seen = new Set()) => {
    const start = base("patinha");
    if (E.build(start).used.has(name)) return start;
    // pose variations: the prefab whose pose names the variable
    for (const p of window.PREFABS) {
      if (!mentions(p.pose, name)) continue;
      const s = base(p.id);
      if (E.build(s).used.has(name)) return s;
    }
    if (seen.has(name)) return null;
    seen.add(name);
    for (const [parent, pdef] of Object.entries(E.VARS)) {
      for (const o of pdef.options) {
        if (!mentions(o.en, name)) continue;
        const c = contextFor(parent, seen);
        if (!c) continue;
        const s = JSON.parse(JSON.stringify(c)); s.values[parent] = o.id;
        if (E.build(s).used.has(name)) return s;
      }
    }
    return null;
  };
  // {{var.field}}: every option of var must carry the field, or the text silently goes empty
  const texts = [...Object.values(window.CATALOG.blocks), ...window.PREFABS.map(p => p.pose),
    ...Object.values(E.VARS).flatMap(d => d.options.map(o => o.en))];
  for (const t of texts) {
    for (const m of String(t).matchAll(/\{\{(\w+)\.(\w+)\}\}/g)) {
      const def = E.VARS[m[1]];
      if (!def) { out.problems.push(`{{${m[1]}.${m[2]}}}: unknown variable`); continue; }
      for (const o of def.options) if (!(m[2] in o)) out.problems.push(`var ${m[1]}=${o.id}: missing field "${m[2]}"`);
    }
  }
  for (const [name, def] of Object.entries(E.VARS)) {
    const ctx = contextFor(name);
    if (!ctx) out.problems.push(`var ${name}: no selection reaches it, so it never shows up`);
    const optIds = new Set();
    for (const o of def.options) {
      if (optIds.has(o.id)) out.problems.push(`var ${name}: duplicate option id ${o.id}`);
      optIds.add(o.id);
      const s = JSON.parse(JSON.stringify(ctx || base("patinha"))); s.values[name] = def.multi ? [o.id] : o.id;
      const r = E.build(s);
      if (/\{\{/.test(r.text)) out.problems.push(`var ${name}=${o.id}: unresolved placeholder`);
      if (glitch(r.text)) out.problems.push(`var ${name}=${o.id}: spacing/punctuation glitch`);
      for (const m of o.en.matchAll(/\{\{list:([\w,]+)\}\}/g)) {
        for (const item of m[1].split(",")) {
          if (!E.VARS[item]) { out.problems.push(`var ${name}=${o.id}: list names unknown variable ${item}`); continue; }
          for (const io of E.VARS[item].options) {
            if (io.en.includes(",")) out.problems.push(`var ${item}=${io.id}: comma inside a list item`);
          }
        }
      }
    }
    const defaults = def.multi ? (Array.isArray(def.default) ? def.default : [null]) : [def.default];
    for (const d of defaults) if (!def.options.some(o => o.id === d)) out.problems.push(`var ${name}: default ${d} is not an option`);
    if (def.multi) {
      // every option alone, and all options together (one per slot), must read cleanly
      const s = JSON.parse(JSON.stringify(ctx || base("patinha")));
      const seen = new Set();
      s.values[name] = def.options.filter(o => !o.slot || (!seen.has(o.slot) && seen.add(o.slot))).map(o => o.id);
      if (glitch(E.build(s).text) || /\{\{/.test(E.build(s).text)) out.problems.push(`var ${name}: glitch with several options at once`);
    }
  }
  // edit request: change eye color and accent color, expect eye color + every slot using the accent
  const a = base("patinha"), b = JSON.parse(JSON.stringify(a));
  b.values.eyeColor = "amber"; b.values.accentColor = "navy";
  const ed = E.editRequest(a, b);
  out.edit = { slots: ed.changes.map(c => c.slot), text: ed.text };
  // custom value
  const c = base("patinha"); c.values.hairColor = E.CUSTOM; c.custom.hairColor = "silver with pink tips";
  out.custom = E.build(c).text.split("\n").find(l => l.startsWith("An adult"));
  out.sample = E.build(base("patinha")).text;
  out.sampleWallpaper = E.build(base("wp-cafe")).text;
  const outfitLine = s => E.build(s).text.split("\n").find(l => l.startsWith("An adult"));
  const b1 = base("patinha");
  Object.assign(b1.values, { outfit: "build", topPattern: "stripes", outerwear: "cardigan", legwear: "none", shoes: "loafers" });
  const b2 = base("patinha");
  Object.assign(b2.values, { outfit: "builddress", dress: "pinafore", topColor: "navy", topPattern: "gingham" });
  out.sampleBuild = [outfitLine(b1), outfitLine(b2)];
  return JSON.stringify(out);
})()
""")
r = json.loads(report)
print("prefabs:", len(r["prefabs"]), "| vars used per prefab:", sorted({u for _, u in r["prefabs"]}))
print("prompt length min/max:", min(r["lengths"]), max(r["lengths"]), "| url-encoded max approx:", int(max(r["lengths"]) * 1.25))
print("problems:", len(r["problems"]))
for p in r["problems"]: print("  -", p)
if "--show" not in sys.argv:
    sys.exit(1 if r["problems"] else 0)
print("\nedit slots:", r["edit"]["slots"])
print(r["edit"]["text"])
print("\ncustom:", r["custom"])
print("\n=== sample (patinha) ===\n" + r["sample"])
print("\n=== sample (wp-cafe) ===\n" + r["sampleWallpaper"])
print("\n=== built outfits ===\n" + "\n".join(r["sampleBuild"]))
