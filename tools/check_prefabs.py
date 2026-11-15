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
    if (r.text.split("\n").some(l => /\s[.,]|\.\.|,,| {2}/.test(l))) out.problems.push(`prefab ${p.id}: spacing/punctuation glitch`);
    out.lengths.push(r.text.length);
    out.prefabs.push([p.id, r.used.size]);
  }
  // every option of every variable, inside one prefab, must resolve cleanly
  for (const [name, def] of Object.entries(E.VARS)) {
    const optIds = new Set();
    for (const o of def.options) {
      if (optIds.has(o.id)) out.problems.push(`var ${name}: duplicate option id ${o.id}`);
      optIds.add(o.id);
      const s = base("patinha"); s.values[name] = o.id;
      const r = E.build(s);
      if (/\{\{/.test(r.text)) out.problems.push(`var ${name}=${o.id}: unresolved placeholder`);
    }
    if (!def.options.some(o => o.id === def.default)) out.problems.push(`var ${name}: default ${def.default} is not an option`);
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
