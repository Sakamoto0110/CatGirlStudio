/* Catgirl Prompt Builder: UI and prompt assembly. Data lives in data/*.js. */
(function () {
  "use strict";

  const C = window.CATALOG;
  const PREFABS = window.PREFABS;
  const CATEGORIES = window.CATEGORIES;
  const E = window.PromptEngine.create(C, PREFABS);
  const VARS = C.vars;
  const CUSTOM = E.CUSTOM;
  const { findPrefab, defaultValues, normalizeValue, build, editRequest } = E;
  const ALL = "Todas";
  const KEY_STATE = "cpb.state.v1";
  const KEY_FAVS = "cpb.favorites.v1";

  /* ------------------------------------------------------------ storage */
  const store = {
    get(key) { try { const raw = localStorage.getItem(key); return raw ? JSON.parse(raw) : null; } catch (e) { return null; } },
    set(key, value) { try { localStorage.setItem(key, JSON.stringify(value)); } catch (e) { /* storage unavailable */ } }
  };

  /* -------------------------------------------------------------- state */
  const state = {
    prefabId: PREFABS[0].id,
    values: defaultValues(),
    custom: {},
    locks: new Set(),
    category: ALL,
    query: "",
    baseline: null
  };

  function applyPrefabDefaults(prefab) {
    // the mascot format belongs to the mascot prefab; leaving it goes back to the portrait format
    if (!("format" in (prefab.defaults || {})) && state.values.format === "mascot" && !state.locks.has("format")) {
      state.values.format = "portrait";
    }
    for (const [name, value] of Object.entries(prefab.defaults || {})) {
      const v = normalizeValue(name, value);
      if (!state.locks.has(name) && v !== undefined) state.values[name] = v;
    }
  }

  // copies lists too, so a saved selection never shares an array with the live state
  const copyValues = values => Object.fromEntries(Object.entries(values).map(([k, v]) => [k, Array.isArray(v) ? [...v] : v]));

  function snapshot() {
    return { prefabId: state.prefabId, values: copyValues(state.values), custom: { ...state.custom } };
  }

  /** A stored selection (favorite, saved state, studio image) with its values in today's shape. */
  function normalizeSelection(sel) {
    const values = {};
    for (const [name, value] of Object.entries((sel && sel.values) || {})) {
      const v = normalizeValue(name, value);
      if (v !== undefined) values[name] = v;
    }
    return { prefabId: sel && sel.prefabId, values, custom: { ...((sel && sel.custom) || {}) } };
  }

  function restore() {
    const saved = store.get(KEY_STATE);
    if (!saved || typeof saved !== "object") { applyPrefabDefaults(findPrefab(state.prefabId)); return; }
    if (saved.prefabId && PREFABS.some(p => p.id === saved.prefabId)) state.prefabId = saved.prefabId;
    Object.assign(state.values, normalizeSelection(saved).values);
    for (const [name, text] of Object.entries(saved.custom || {})) if (VARS[name] && typeof text === "string") state.custom[name] = text;
    for (const name of saved.locks || []) if (VARS[name]) state.locks.add(name);
    if (saved.baseline && saved.baseline.values) state.baseline = normalizeSelection(saved.baseline);
  }

  function persist() {
    store.set(KEY_STATE, { ...snapshot(), locks: [...state.locks], baseline: state.baseline });
  }

  /* ------------------------------------------------------------- helpers */
  const $ = id => document.getElementById(id);
  function el(tag, attrs, ...children) {
    const node = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs || {})) {
      if (v === null || v === undefined || v === false) continue;
      if (k === "class") node.className = v;
      else if (k === "text") node.textContent = v;
      else if (k.startsWith("on")) node.addEventListener(k.slice(2), v);
      else node.setAttribute(k, v === true ? "" : v);
    }
    for (const child of children.flat()) if (child !== null && child !== undefined) node.append(child);
    return node;
  }
  function icon(name) {
    const paths = {
      lock: '<rect x="4" y="9" width="12" height="9" rx="2"/><path d="M7 9V6.5a3 3 0 0 1 6 0V9" fill="none" stroke="currentColor" stroke-width="1.8"/>',
      unlock: '<rect x="4" y="9" width="12" height="9" rx="2"/><path d="M7 9V6.5a3 3 0 0 1 5.8-1.1" fill="none" stroke="currentColor" stroke-width="1.8"/>',
      dice: '<rect x="3" y="3" width="14" height="14" rx="3" fill="none" stroke="currentColor" stroke-width="1.8"/><circle cx="7" cy="7" r="1.4"/><circle cx="13" cy="13" r="1.4"/><circle cx="10" cy="10" r="1.4"/>'
    };
    const span = document.createElement("span");
    span.innerHTML = '<svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">' + paths[name] + "</svg>";
    return span.firstChild;
  }
  function pick(list) { return list[Math.floor(Math.random() * list.length)]; }

  let statusTimer = 0;
  function say(message) {
    const node = $("status");
    node.textContent = message;
    clearTimeout(statusTimer);
    statusTimer = setTimeout(() => { node.textContent = ""; }, 4000);
  }

  function copyText(text, fallbackNode, okMessage) {
    const fallback = () => {
      try {
        const range = document.createRange();
        range.selectNodeContents(fallbackNode);
        const sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
      } catch (e) { /* ignore */ }
      say("Não consegui copiar sozinho. O texto está selecionado: use Ctrl+C.");
    };
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(() => say(okMessage), fallback);
      } else fallback();
    } catch (e) { fallback(); }
  }

  /* -------------------------------------------------------------- render */
  function renderShelf() {
    const cats = $("pose-cats");
    cats.replaceChildren(...[ALL, ...CATEGORIES].map(cat => el("button", {
      class: "chip", type: "button", "aria-pressed": String(state.category === cat), text: cat,
      onclick: () => { state.category = cat; renderShelf(); }
    })));

    const q = state.query.trim().toLowerCase();
    const list = PREFABS.filter(p =>
      (state.category === ALL || p.category === state.category) &&
      (!q || (p.name + " " + p.desc + " " + p.category).toLowerCase().includes(q)));

    $("pose-count").textContent = list.length + " de " + PREFABS.length;
    const ul = $("pose-list");
    if (!list.length) { ul.replaceChildren(el("li", { class: "empty", text: "Nenhuma pose com esse nome." })); return; }
    ul.replaceChildren(...list.map(p => el("li", {},
      el("button", {
        class: "pose", type: "button", "aria-current": String(p.id === state.prefabId),
        onclick: () => selectPrefab(p.id)
      },
        el("span", { class: "cat", text: p.category }),
        el("span", { class: "name", text: p.name }),
        el("span", { class: "desc", text: p.desc })
      ))));
  }

  function renderSheet(used) {
    const sheet = $("sheet");
    const groups = C.groups.map(group => {
      const names = Object.keys(VARS).filter(n => VARS[n].group === group && used.has(n));
      if (!names.length) return null;
      return el("fieldset", {},
        el("legend", { text: group }),
        el("div", { class: "fields" }, names.map(renderField)));
    });
    sheet.replaceChildren(...groups.filter(Boolean));
  }

  // a random value: one option, or for a multi-choice field up to two that do not share a slot
  function randomValue(name) {
    const def = VARS[name];
    if (!def.multi) return pick(def.options).id;
    const count = Math.floor(Math.random() * 3);
    const out = [], slots = new Set();
    for (const o of [...def.options].sort(() => Math.random() - 0.5)) {
      if (out.length >= count) break;
      if (o.slot && slots.has(o.slot)) continue;
      out.push(o.id);
      if (o.slot) slots.add(o.slot);
    }
    return out;
  }

  function toggleOption(name, opt) {
    const def = VARS[name];
    const current = state.values[name] || [];
    let next = current.filter(id => id !== opt.id);
    if (next.length === current.length) {
      // one per slot: picking a second hat takes the first one off
      if (opt.slot) next = next.filter(id => (def.options.find(o => o.id === id) || {}).slot !== opt.slot);
      next.push(opt.id);
    }
    state.values[name] = next;
    update(true);
    const chip = $("m-" + name + "-" + opt.id);
    if (chip) chip.focus();
  }

  function renderField(name) {
    const def = VARS[name];
    const value = state.values[name];
    const locked = state.locks.has(name);
    const selectId = "f-" + name;

    const dice = el("button", {
      class: "icon-btn", type: "button", title: "Sortear " + def.label.toLowerCase(), "aria-label": "Sortear " + def.label,
      onclick: () => { state.values[name] = randomValue(name); update(true); }
    }, icon("dice"));
    const lock = el("button", {
      class: "icon-btn", type: "button", "aria-pressed": String(locked),
      title: locked ? "Destravar" : "Travar (não muda ao trocar de pose ou sortear)",
      "aria-label": (locked ? "Destravar " : "Travar ") + def.label,
      onclick: () => { locked ? state.locks.delete(name) : state.locks.add(name); update(true); }
    }, icon(locked ? "lock" : "unlock"));

    if (def.multi) {
      const chosen = new Set(value || []);
      const labelId = "l-" + name;
      return el("div", { class: "field field-multi" + (locked ? " locked" : "") },
        el("div", { class: "field-head" }, el("span", { class: "field-label", id: labelId, text: def.label }), dice, lock),
        el("div", { class: "multi", role: "group", "aria-labelledby": labelId },
          def.options.map(o => el("button", {
            class: "chip", type: "button", id: "m-" + name + "-" + o.id,
            "aria-pressed": String(chosen.has(o.id)), text: o.pt, onclick: () => toggleOption(name, o)
          }))));
    }

    const select = el("select", {
      id: selectId,
      onchange: e => { state.values[name] = e.target.value; update(true); if (e.target.value === CUSTOM) setTimeout(() => { const t = $("c-" + name); if (t) t.focus(); }, 0); }
    },
      def.options.map(o => el("option", { value: o.id, selected: o.id === value, text: o.pt })),
      el("option", { value: CUSTOM, selected: value === CUSTOM, text: "Personalizado (escrever em inglês)…" }));

    const custom = value === CUSTOM ? el("input", {
      id: "c-" + name, type: "text", value: state.custom[name] || "",
      placeholder: "ex.: " + (def.options.find(o => o.id === def.default) || def.options[0]).en.slice(0, 60),
      "aria-label": def.label + " (texto livre)",
      oninput: e => { state.custom[name] = e.target.value; update(false); }
    }) : null;

    return el("div", { class: "field" + (locked ? " locked" : "") },
      el("label", { for: selectId, text: def.label }),
      el("div", { class: "field-row" }, select, dice, lock),
      custom);
  }

  function renderPrompt(result) {
    const pre = $("prompt");
    // highlight unresolved placeholders, if any slipped through
    pre.replaceChildren(...result.text.split(/(\{\{\w+\}\})/g).map(part =>
      /^\{\{\w+\}\}$/.test(part) ? el("span", { class: "ph", text: part }) : document.createTextNode(part)));

    $("char-count").textContent = result.text.length.toLocaleString("pt-BR") + " caracteres";
    const fmt = VARS.format.options.find(o => o.id === state.values.format);
    const size = {
      portrait: "1024×1536", mascot: "1024×1536", landscape: "1536×1024", square: "1024×1024",
      wide: "2560×1080", fhd: "1920×1080", vertical: "1080×1920"
    }[state.values.format] || "tamanho livre";
    $("meta").replaceChildren(
      el("span", { class: "tag", text: findPrefab(state.prefabId).name }),
      el("span", { class: "tag", text: size }),
      el("span", { class: "tag", text: (fmt ? fmt.pt.split(" (")[0] : "Formato livre") }),
      el("span", { class: "tag", text: result.used.size + " variáveis" }));

    const href = "https://chatgpt.com/?q=" + encodeURIComponent(result.text);
    $("btn-gpt").href = href;
    $("dock-gpt").href = href;
  }

  function renderEdit() {
    const hint = $("edit-hint");
    const box = $("edit-changes");
    const pre = $("edit-prompt");
    const actions = $("edit-actions");
    if (!state.baseline) {
      hint.textContent = "Depois de copiar ou abrir um prompt, mude o que quiser aqui e eu monto o pedido de ajuste, que mantém o resto da imagem igual.";
      box.replaceChildren(); pre.hidden = true; actions.hidden = true;
      return;
    }
    const req = editRequest(state.baseline, snapshot());
    if (!req.changes.length) {
      hint.textContent = "Nada mudou desde o último prompt enviado. Troque uma opção para gerar o pedido de ajuste.";
      box.replaceChildren(); pre.hidden = true; actions.hidden = true;
      return;
    }
    hint.textContent = "Mudou desde o último prompt enviado:";
    box.replaceChildren(...req.changes.map(c => el("li", { text: c.labelPt })));
    pre.textContent = req.text;
    pre.hidden = false; actions.hidden = false;
  }

  let lastResult = null;
  function update(reRenderSheet) {
    lastResult = build(state);
    if (reRenderSheet) renderSheet(lastResult.used);
    renderPrompt(lastResult);
    renderEdit();
    persist();
  }

  function selectPrefab(id) {
    state.prefabId = id;
    applyPrefabDefaults(findPrefab(id));
    renderShelf();
    update(true);
  }

  /* ----------------------------------------------------------- favorites */
  function loadFavs() { const f = store.get(KEY_FAVS); return Array.isArray(f) ? f : []; }
  function renderFavs() {
    const favs = loadFavs();
    const ul = $("favs");
    if (!favs.length) { ul.replaceChildren(el("li", { class: "empty", text: "Nenhum favorito ainda." })); return; }
    ul.replaceChildren(...favs.map((f, i) => {
      const del = el("button", { class: "btn small", type: "button", text: "Apagar" });
      del.addEventListener("click", () => {
        if (del.dataset.armed) { const all = loadFavs(); all.splice(i, 1); store.set(KEY_FAVS, all); renderFavs(); say("Favorito apagado."); }
        else { del.dataset.armed = "1"; del.textContent = "Confirmar"; setTimeout(() => { if (del.isConnected) { delete del.dataset.armed; del.textContent = "Apagar"; } }, 3000); }
      });
      return el("li", {},
        el("span", { class: "fav-name", title: f.name, text: f.name }),
        el("span", { class: "fav-pose", text: findPrefab(f.prefabId).name }),
        el("button", { class: "btn small", type: "button", text: "Carregar", onclick: () => loadFav(f) }),
        del);
    }));
  }
  function loadSelection(raw) {
    const sel = normalizeSelection(raw);
    if (PREFABS.some(p => p.id === sel.prefabId)) state.prefabId = sel.prefabId;
    Object.assign(state.values, sel.values);
    state.custom = sel.custom;
    renderShelf(); update(true);
  }
  function loadFav(f) {
    loadSelection(f);
    say("Favorito “" + f.name + "” carregado.");
  }

  /* -------------------------------------------------------------- events */
  function sendPrompt(okMessage) {
    state.baseline = snapshot();
    copyText(lastResult.text, $("prompt"), okMessage);
    renderEdit(); persist();
  }

  function randomizeFields() {
    const touched = new Set();
    for (let pass = 0; pass < 3; pass++) {
      for (const name of build(state).used) {
        if (state.locks.has(name) || touched.has(name)) continue;
        state.values[name] = randomValue(name);
        touched.add(name);
      }
    }
    update(true);
    say("Campos sorteados (os travados ficaram como estavam).");
  }

  function wire() {
    $("pose-search").addEventListener("input", e => { state.query = e.target.value; renderShelf(); });
    $("btn-copy").addEventListener("click", () => sendPrompt("Prompt copiado. Cole no ChatGPT."));
    $("dock-copy").addEventListener("click", () => sendPrompt("Prompt copiado. Cole no ChatGPT."));
    $("btn-gpt").addEventListener("click", () => sendPrompt("Abrindo o ChatGPT. O prompt também foi copiado."));
    $("dock-gpt").addEventListener("click", () => sendPrompt("Abrindo o ChatGPT. O prompt também foi copiado."));
    $("btn-copy-edit").addEventListener("click", () => {
      const req = editRequest(state.baseline, snapshot());
      copyText(req.text, $("edit-prompt"), "Pedido de ajuste copiado.");
      state.baseline = snapshot(); renderEdit(); persist();
    });
    $("btn-rebase").addEventListener("click", () => { state.baseline = snapshot(); renderEdit(); persist(); say("O prompt atual virou a nova base."); });
    $("btn-random").addEventListener("click", randomizeFields);
    $("btn-random-pose").addEventListener("click", () => {
      const others = PREFABS.filter(p => p.id !== state.prefabId);
      selectPrefab(pick(others).id);
      say("Pose sorteada: " + findPrefab(state.prefabId).name + ".");
    });
    $("btn-reset").addEventListener("click", () => {
      state.values = defaultValues(); state.custom = {}; state.locks.clear(); state.prefabId = PREFABS[0].id;
      applyPrefabDefaults(findPrefab(state.prefabId));
      renderShelf(); update(true); say("Tudo voltou ao padrão.");
    });
    $("fav-form").addEventListener("submit", e => {
      e.preventDefault();
      const input = $("fav-name");
      const name = input.value.trim() || findPrefab(state.prefabId).name + " " + new Date().toLocaleDateString("pt-BR");
      const favs = loadFavs();
      favs.unshift({ name, ...snapshot(), at: Date.now() });
      store.set(KEY_FAVS, favs.slice(0, 60));
      input.value = ""; renderFavs(); say("Salvo nos favoritos.");
    });
  }

  /* ---------------------------------------------------------------- boot */
  restore();
  wire();
  renderShelf();
  renderFavs();
  update(true);

  // exposed for quick checks from the console, and for studio.js (local studio only)
  window.CPB = {
    build: s => build(s || state), editRequest, state, PREFABS, VARS, snapshot, loadSelection, say,
    setBaseline(sel) { state.baseline = normalizeSelection(JSON.parse(JSON.stringify(sel))); renderEdit(); persist(); }
  };
})();
