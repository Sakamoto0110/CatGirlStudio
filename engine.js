/*
 * Catgirl Prompt Builder: prompt engine (no DOM, so it can be tested on its own).
 *
 * A "selection" is { prefabId, values: {var: optionId | "__custom"}, custom: {var: text} }.
 */
window.PromptEngine = (function () {
  "use strict";
  const CUSTOM = "__custom";
  const PLACEHOLDER = /\{\{(\w+)\}\}/g;
  // {{list:a,b,c}} expands each name, drops the empty ones and joins the rest as "x, y and z"
  const LIST = /\{\{list:(\w+(?:,\w+)*)\}\}/g;
  // {{var.field}} is another field of the option chosen for var (e.g. {{focus.bg}}), so one
  // choice can steer text elsewhere in the prompt
  const FIELD = /\{\{(\w+)\.(\w+)\}\}/g;
  // stands where an option said nothing, until tidy() removes it with its joining word
  const EMPTY = "\uE000";

  function joinList(items) {
    if (items.length < 2) return items.join("");
    return items.slice(0, -1).join(", ") + " and " + items[items.length - 1];
  }

  function create(C, PREFABS) {
    const VARS = C.vars;
    const has = (obj, key) => Object.prototype.hasOwnProperty.call(obj, key);

    function findPrefab(id) { return PREFABS.find(p => p.id === id) || PREFABS[0]; }

    function defaultValues() {
      const v = {};
      for (const [name, def] of Object.entries(VARS)) v[name] = Array.isArray(def.default) ? [...def.default] : def.default;
      return v;
    }

    function isMulti(name) { return has(VARS, name) && !!VARS[name].multi; }

    /**
     * The value in the shape its variable keeps, or undefined if it is not valid there.
     * A multi-choice variable holds a list of option ids; an old single id (or "none",
     * saved before the variable became multi-choice) is turned into a list.
     */
    function normalizeValue(name, value) {
      if (!has(VARS, name)) return undefined;
      const ids = VARS[name].options.map(o => o.id);
      if (isMulti(name)) {
        const list = Array.isArray(value) ? value : typeof value === "string" ? [value] : null;
        return list ? [...new Set(list.filter(id => ids.includes(id)))] : undefined;
      }
      return value === CUSTOM || ids.includes(value) ? value : undefined;
    }

    function isValidValue(name, value) { return normalizeValue(name, value) !== undefined; }

    function optionText(name, s) {
      const def = VARS[name];
      const value = s.values[name];
      if (def.multi) {
        // catalog order, not click order, so the same choices always read the same
        const chosen = Array.isArray(value) ? value : [];
        return def.options.filter(o => chosen.includes(o.id)).map(o => o.en).join(" ");
      }
      if (value === CUSTOM) return ((s.custom && s.custom[name]) || "").trim();
      const opt = def.options.find(o => o.id === value) || def.options.find(o => o.id === def.default);
      return opt ? opt.en : "";
    }

    // a custom (typed) value has no extra fields, so it falls back to the default option's
    function optionField(name, field, s) {
      const def = VARS[name];
      const chosen = def.options.find(o => o.id === s.values[name]);
      const fallback = def.options.find(o => o.id === def.default);
      const opt = chosen && has(chosen, field) ? chosen : fallback;
      return opt && has(opt, field) ? String(opt[field]) : "";
    }

    function expand(text, s, used, depth) {
      depth = depth || 0;
      return String(text).replace(LIST, (match, names) => {
        if (depth > 12) return "";
        const items = names.split(",").map(n => tidy(expand("{{" + n + "}}", s, used, depth + 1))).filter(Boolean);
        return items.length ? joinList(items) : EMPTY; // an all-empty list also drops its joining word
      }).replace(FIELD, (match, name, field) => {
        if (depth > 12 || !has(VARS, name)) return depth > 12 ? "" : match;
        if (used) used.add(name);
        return expand(optionField(name, field, s), s, used, depth + 1);
      }).replace(PLACEHOLDER, (match, name) => {
        if (depth > 12) return "";
        if (name === "pose") return expand(findPrefab(s.prefabId).pose, s, used, depth + 1);
        if (has(C.blocks, name)) return expand(C.blocks[name], s, used, depth + 1);
        if (has(VARS, name)) {
          if (used) used.add(name);
          const text = expand(optionText(name, s), s, used, depth + 1);
          return text.trim() ? text : EMPTY;
        }
        return match; // unknown placeholder stays visible so it gets noticed
      });
    }

    function tidy(text) {
      return text
        .split("\n")
        .map(line => line
          // an option left empty ("none") takes its joining word with it: "shorts and {{legwear}}." -> "shorts."
          .replace(new RegExp(",?\\s+(?:and|with|over)\\s*" + EMPTY, "g"), "")
          .replace(new RegExp(EMPTY, "g"), "")
          .replace(/[ \t]+/g, " ")
          .replace(/ +([.,;:!?])/g, "$1")
          .replace(/\.\.(?!\.)/g, ".")
          .trim())
        .join("\n")
        .replace(/\n{3,}/g, "\n\n")
        .trim();
    }

    /** Full prompt for a selection, plus the set of variables it reached. */
    function build(s) {
      const used = new Set();
      const text = tidy(expand(C.blocks.prompt, s, used));
      return { text, used };
    }

    // Top-level slots: variables written directly in the blocks, plus the pose.
    // Nested variables (colors inside an outfit) show up through their slot's text.
    const SLOTS = (() => {
      const names = new Set();
      for (const text of Object.values(C.blocks)) {
        for (const m of String(text).matchAll(PLACEHOLDER)) if (has(VARS, m[1])) names.add(m[1]);
      }
      return ["pose", ...names];
    })();

    function slotText(slot, s) {
      if (slot === "pose") return tidy(expand(findPrefab(s.prefabId).pose, s, null));
      return tidy(expand(optionText(slot, s), s, null));
    }

    // A slot whose chosen option is built from parts (it holds a {{list:}}, like the built
    // outfit or hairstyle) and did not change between the two selections is compared part by
    // part, so the edit names only the part that changed. The parts are the list items plus
    // any other variable written directly in that option.
    function subSlots(slot, a, b) {
      if (slot === "pose" || a.values[slot] !== b.values[slot] || a.values[slot] === CUSTOM) return [slot];
      const opt = VARS[slot].options.find(o => o.id === a.values[slot]);
      if (!opt || !/\{\{list:/.test(opt.en)) return [slot];
      const parts = [];
      for (const m of opt.en.matchAll(/\{\{(?:list:)?(\w+(?:,\w+)*)\}\}/g)) {
        for (const n of m[1].split(",")) if (has(VARS, n)) parts.push(n);
      }
      return parts.length ? parts : [slot];
    }

    // For a multi-choice slot whose set of choices changed, say what to add and what to take
    // off, so the edit leaves the other accessories alone.
    function multiChange(slot, base, current) {
      const was = new Set(base.values[slot] || []), now = new Set(current.values[slot] || []);
      const sentences = opts => tidy(opts.map(o => expand(o.en, current, null)).join(" "));
      const added = VARS[slot].options.filter(o => now.has(o.id) && !was.has(o.id));
      const removed = VARS[slot].options.filter(o => was.has(o.id) && !now.has(o.id));
      if (!added.length && !removed.length) return null;
      return [added.length ? "add: " + sentences(added) : "", removed.length ? "take off: " + sentences(removed) : ""]
        .filter(Boolean).join(" ");
    }

    /** "Edit the last image, change only..." request between two selections. */
    function editRequest(base, current) {
      const changes = [];
      for (const slot of new Set(SLOTS.flatMap(sl => subSlots(sl, base, current)))) {
        const before = slotText(slot, base);
        let after = slotText(slot, current);
        if (before === after) continue;
        if (isMulti(slot)) after = multiChange(slot, base, current) || after;
        changes.push({
          slot,
          label: slot === "pose" ? "pose" : VARS[slot].name,
          labelPt: slot === "pose" ? "Pose: " + findPrefab(current.prefabId).name : VARS[slot].label,
          after
        });
      }
      if (!changes.length) return { changes, text: "" };
      const text = [
        "Edit the last image. Keep everything else exactly the same: the same character, face, outfit, pose, framing, art style, colors and background.",
        "Change only the following:",
        ...changes.map(c => "- " + c.label + ": " + (c.after || "remove it")),
        "Do not change anything else."
      ].join("\n");
      return { changes, text };
    }

    return { CUSTOM, VARS, PREFABS, findPrefab, defaultValues, isMulti, normalizeValue, isValidValue, optionText, expand, tidy, build, SLOTS, editRequest };
  }

  return { create, CUSTOM };
})();
