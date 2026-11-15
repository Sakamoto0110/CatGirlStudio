/*
 * Catgirl Prompt Builder: prompt engine (no DOM, so it can be tested on its own).
 *
 * A "selection" is { prefabId, values: {var: optionId | "__custom"}, custom: {var: text} }.
 */
window.PromptEngine = (function () {
  "use strict";
  const CUSTOM = "__custom";
  const PLACEHOLDER = /\{\{(\w+)\}\}/g;

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
      return String(text).replace(PLACEHOLDER, (match, name) => {
        if (depth > 12) return "";
        if (name === "pose") return expand(findPrefab(s.prefabId).pose, s, used, depth + 1);
        if (has(C.blocks, name)) return expand(C.blocks[name], s, used, depth + 1);
        if (has(VARS, name)) {
          if (used) used.add(name);
          return expand(optionText(name, s), s, used, depth + 1);
        }
        return match; // unknown placeholder stays visible so it gets noticed
      });
    }

    function tidy(text) {
      return text
        .split("\n")
        .map(line => line.replace(/[ \t]+/g, " ").replace(/ +([.,;:!?])/g, "$1").replace(/\.\.(?!\.)/g, ".").trim())
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

    /** "Edit the last image, change only..." request between two selections. */
    function editRequest(base, current) {
      const changes = [];
      for (const slot of SLOTS) {
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
