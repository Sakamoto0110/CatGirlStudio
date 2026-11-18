/*
 * Catgirl Prompt Builder: local studio panel. Loaded only by tools/studio.py, never by
 * the published page. It generates and edits images through the local server, which
 * holds the OpenAI key and saves every image with its prompt and selection.
 */
(function () {
  "use strict";
  const CPB = window.CPB;
  if (!CPB) return;

  const KEY_CURRENT = "cpb.studio.current";
  const $ = id => document.getElementById(id);
  const store = {
    get(k) { try { return localStorage.getItem(k); } catch (e) { return null; } },
    set(k, v) { try { localStorage.setItem(k, v); } catch (e) { /* storage unavailable */ } }
  };
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
  const money = v => "US$ " + Number(v || 0).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 3 })
    + (info && info.fake ? " (simulado)" : "");

  let info = null;      // /api/status
  let current = null;   // image the quick edit applies to
  let gallery = [];
  let busy = false;

  async function api(path, body) {
    const opts = body ? { method: "POST", headers: { "Content-Type": "application/json", "X-Studio": "1" }, body: JSON.stringify(body) } : {};
    const res = await fetch(path, opts);
    let data;
    try { data = await res.json(); } catch (e) { data = { error: "O servidor respondeu algo inesperado (" + res.status + ")." }; }
    if (!res.ok || data.error) throw new Error(data.error || "Erro " + res.status + ".");
    return data;
  }

  /* --------------------------------------------------------------- style */
  document.head.append(el("style", {}, `
.studio-opts { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
.studio-opts label { display: grid; gap: 3px; font-size: .8rem; color: var(--muted); min-width: 0; }
.studio-opts select { width: 100%; min-width: 0; padding: 6px 8px; border: 1px solid var(--line); border-radius: 8px; background: var(--bg); }
.studio-msg { margin: 0; font-size: .85rem; color: var(--muted); min-height: 1.3em; }
.studio-msg.err { color: var(--accent); }
.studio-cur { margin: 0; display: grid; gap: 8px; }
.studio-cur img, .studio-thumbs img {
  display: block; width: 100%; border-radius: 8px; border: 1px solid var(--line);
  background: repeating-conic-gradient(var(--surface-2) 0 25%, var(--surface) 0 50%) 0 0 / 16px 16px;
}
.studio-cur img { max-height: 52dvh; object-fit: contain; }
.studio-cur figcaption { font-size: .8rem; color: var(--muted); display: flex; flex-wrap: wrap; gap: 6px 10px; align-items: center; }
.studio-thumbs { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(64px, 1fr)); gap: 6px; }
.studio-thumbs button { padding: 0; border: 2px solid transparent; border-radius: 10px; background: none; cursor: pointer; width: 100%; }
.studio-thumbs button[aria-current="true"] { border-color: var(--accent); }
.studio-thumbs img { aspect-ratio: 2 / 3; object-fit: cover; }
.studio-busy { opacity: .6; pointer-events: none; }
`));

  /* --------------------------------------------------------------- panel */
  const modelSel = el("select", { id: "st-model" });
  const qualitySel = el("select", { id: "st-quality" });
  const countSel = el("select", { id: "st-n" }, [1, 2, 3, 4].map(n => el("option", { value: String(n), text: n === 1 ? "1 imagem" : n + " imagens" })));
  const genBtn = el("button", { class: "btn primary", type: "button", id: "st-generate", onclick: generate });
  const msg = el("p", { class: "studio-msg", id: "st-msg", role: "status", "aria-live": "polite" });
  const figure = el("figure", { class: "studio-cur", id: "st-current", hidden: true });
  const thumbs = el("ul", { class: "studio-thumbs", id: "st-thumbs" });
  const panel = el("div", { class: "sub", id: "studio" },
    el("h3", { text: "Gerar pela API" }),
    el("div", { class: "studio-opts" },
      el("label", { for: "st-model" }, "Modelo", modelSel),
      el("label", { for: "st-quality" }, "Qualidade", qualitySel),
      el("label", { for: "st-n" }, "Quantidade", countSel)),
    el("div", { class: "actions" }, genBtn),
    msg, figure,
    el("h3", { text: "Geradas recentemente" }),
    thumbs);
  $("edit-box").before(panel);

  const applyBtn = el("button", { class: "btn primary small", type: "button", id: "st-apply", onclick: applyEdit });
  $("edit-actions").prepend(applyBtn);

  function say(text, isError) { msg.textContent = text; msg.classList.toggle("err", !!isError); }

  /* ------------------------------------------------------------- pricing */
  function sizeNow() { return info.sizes[CPB.state.values.format] || info.sizes.portrait; }
  function outputCost(quality, size, n) { return (info.outTokens[quality + " " + size] || 0) * info.prices.output * n; }
  function refreshLabels() {
    if (!info) return;
    const n = +countSel.value;
    genBtn.textContent = (n === 1 ? "Gerar imagem" : "Gerar " + n + " imagens") + " · ~" + money(outputCost(qualitySel.value, sizeNow(), n));
    const size = current ? current.size : sizeNow();
    applyBtn.textContent = "Aplicar nesta imagem · ~" + money(outputCost(qualitySel.value, size, 1)) + " + entrada";
    applyBtn.disabled = !current || busy;
    applyBtn.title = current ? "Manda a imagem selecionada e o pedido de ajuste para a API" : "Gere ou escolha uma imagem primeiro";
    genBtn.disabled = busy || !info.key;
  }
  new MutationObserver(refreshLabels).observe($("prompt"), { childList: true, characterData: true, subtree: true });
  [qualitySel, countSel, modelSel].forEach(s => s.addEventListener("change", refreshLabels));

  /* ------------------------------------------------------------- current */
  function setCurrent(img) {
    current = img;
    if (img) store.set(KEY_CURRENT, img.id);
    figure.hidden = !img;
    if (!img) return;
    const sel = img.selection || {};
    const prefab = CPB.PREFABS.find(p => p.id === sel.prefabId);
    const twoX = img.upscaled
      ? el("a", { class: "btn small", href: img.upscaled, target: "_blank", rel: "noopener", text: "Abrir 2x" })
      : el("button", { class: "btn small", type: "button", text: "Fazer 2x", onclick: () => upscale(img) });
    figure.replaceChildren(
      el("a", { href: img.url, target: "_blank", rel: "noopener", title: "Abrir em tamanho real" },
        el("img", { src: img.url, alt: "Imagem " + (img.kind === "edit" ? "ajustada" : "gerada") + (prefab ? ": " + prefab.name : "") })),
      el("figcaption", {},
        el("span", { text: (img.kind === "edit" ? "Ajuste" : "Gerada") + " · " + (info.models[img.model] || img.model) + " · " + img.quality + " · " + (img.savedSize || img.size).replace("x", "×") + (img.costUsd != null ? " · " + money(img.costUsd) : "") }),
        twoX));
    renderThumbs();
    refreshLabels();
  }

  function renderThumbs() {
    if (!gallery.length) { thumbs.replaceChildren(el("li", { class: "empty", text: "Nenhuma imagem ainda." })); return; }
    thumbs.replaceChildren(...gallery.map(img => el("li", {},
      el("button", {
        type: "button", "aria-current": String(!!current && current.id === img.id),
        title: "Usar esta imagem: volta os campos para como ela foi gerada", onclick: () => pick(img)
      }, el("img", { src: img.thumb, alt: (img.kind === "edit" ? "Ajuste " : "Imagem ") + img.id, loading: "lazy" })))));
  }

  function pick(img) {
    if (busy) return;
    if (img.selection && img.selection.values) { CPB.loadSelection(img.selection); CPB.setBaseline(img.selection); }
    setCurrent(img);
    say("Imagem escolhida. Os campos voltaram para como ela foi feita; mude algo e use o Ajuste rápido.");
  }

  async function loadGallery() {
    gallery = (await api("/api/gallery")).images;
    renderThumbs();
  }

  /* ------------------------------------------------------------- actions */
  async function run(label, call) {
    busy = true; panel.classList.add("studio-busy"); refreshLabels();
    const t0 = Date.now();
    const tick = () => say(label + "… " + Math.round((Date.now() - t0) / 1000) + " s (pode levar até 2 minutos)");
    tick();
    const timer = setInterval(tick, 1000);
    try {
      const r = await call();
      await loadGallery();
      setCurrent(gallery.find(g => g.id === r.images[0].id) || r.images[0]);
      say("Pronto: " + money(r.costUsd) + (r.images.length > 1 ? " pelas " + r.images.length + " imagens" : "") + ". Nesta sessão: " + money(r.spent) + ". Salvo em " + info.outDir + ".");
      return r;
    } catch (e) {
      say(e.message, true);
      return null;
    } finally {
      clearInterval(timer);
      busy = false; panel.classList.remove("studio-busy"); refreshLabels();
    }
  }

  async function generate() {
    const snap = CPB.snapshot();
    const body = {
      prompt: CPB.build().text, selection: snap, model: modelSel.value, quality: qualitySel.value,
      format: info.sizes[snap.values.format] ? snap.values.format : "portrait",
      transparent: snap.values.background === "transparent", n: +countSel.value
    };
    if (await run("Gerando", () => api("/api/generate", body))) CPB.setBaseline(snap);
  }

  async function applyEdit() {
    if (!current || !CPB.state.baseline) return;
    const snap = CPB.snapshot();
    const req = CPB.editRequest(CPB.state.baseline, snap);
    if (!req.text) { say("Nada mudou desde esta imagem: troque alguma opção primeiro.", true); return; }
    const body = {
      parent: current.id, prompt: req.text.replace(/^Edit the last image\./, "Edit this image."),
      selection: snap, model: modelSel.value, quality: qualitySel.value
    };
    if (await run("Ajustando", () => api("/api/edit", body))) CPB.setBaseline(snap);
  }

  async function upscale(img) {
    if (busy) return;
    busy = true; panel.classList.add("studio-busy"); say("Limpando o fundo e fazendo o 2x…");
    try {
      const r = await api("/api/upscale", { id: img.id });
      img.upscaled = r.upscaled;
      const g = gallery.find(x => x.id === img.id); if (g) g.upscaled = r.upscaled;
      say("2x pronto.");
    } catch (e) { say(e.message, true); }
    finally { busy = false; panel.classList.remove("studio-busy"); setCurrent(img); }
  }

  /* ---------------------------------------------------------------- boot */
  (async () => {
    try {
      info = await api("/api/status");
      modelSel.replaceChildren(...Object.entries(info.models).map(([id, name]) => el("option", { value: id, text: name })));
      qualitySel.replaceChildren(...info.qualities.map(q => el("option", { value: q, text: q, selected: q === "high" })));
      if (!info.key) say("Não achei a chave da OpenAI: crie a variável de ambiente OpenAI_CatGirl_API e reinicie o estúdio.", true);
      else if (info.fake) say("Modo de teste: as imagens são desenhadas pelo servidor, nada é cobrado.");
      await loadGallery();
      const saved = store.get(KEY_CURRENT);
      const img = gallery.find(g => g.id === saved);
      if (img) setCurrent(img);
      refreshLabels();
    } catch (e) {
      say("O servidor do estúdio não respondeu: " + e.message, true);
    }
  })();
})();
