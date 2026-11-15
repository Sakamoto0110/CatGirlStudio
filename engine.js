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
      for (const [name, def] of Object.entries(VARS)) v[name] = def.default;
      return v;
    }

    function isValidValue(name, value) {
      return has(VARS, name) && (value === CUSTOM || VARS[name].options.some(o => o.id === value));
    }

    function optionText(name, s) {
      const def = VARS[name];
      const value = s.values[name];
      if (value === CUSTOM) return ((s.custom && s.custom[name]) || "").trim();
      const opt = def.options.find(o => o.id === value) || def.options.find(o => o.id === def.default);
      return opt ? opt.en : "";
    }

    function expand(text, s, used, depth) {
      depth = depth || 0;
      return String(text).replace(LIST, (match, names) => {
        if (depth > 12) return "";
        return joinList(names.split(",").map(n => tidy(expand("{{" + n + "}}", s, used, depth + 1))).filter(Boolean));
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

    // A slot whose chosen option is a {{list:}} (a built outfit) and did not change between
    // the two selections is compared piece by piece, so the edit names only the piece that changed.
    function subSlots(slot, a, b) {
      if (slot === "pose" || a.values[slot] !== b.values[slot] || a.values[slot] === CUSTOM) return [slot];
      const opt = VARS[slot].options.find(o => o.id === a.values[slot]);
      const m = opt && /\{\{list:(\w+(?:,\w+)*)\}\}/.exec(opt.en);
      return m ? m[1].split(",").filter(n => has(VARS, n)) : [slot];
    }

    /** "Edit the last image, change only..." request between two selections. */
    function editRequest(base, current) {
      const changes = [];
      for (const slot of SLOTS.flatMap(sl => subSlots(sl, base, current))) {
        const before = slotText(slot, base);
        const after = slotText(slot, current);
        if (before === after) continue;
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

    return { CUSTOM, VARS, PREFABS, findPrefab, defaultValues, isValidValue, optionText, expand, tidy, build, SLOTS, editRequest };
  }

  return { create, CUSTOM };
})();
