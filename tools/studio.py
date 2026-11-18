"""Local studio: the prompt builder page with buttons that generate and edit images
through the OpenAI Images API. Every image is saved with a .json next to it holding
the prompt, the selection on the page, the model, the quality and what it cost.

    python tools/studio.py              # opens on http://127.0.0.1:8770
    python tools/studio.py --fake       # no API calls: answers with drawn test images
    python tools/studio.py --out D:\\catgirls --port 8800

The API key comes from the OpenAI_CatGirl_API environment variable. On Windows it is
also read from the user's saved variables, so a key saved after this terminal opened
still works. The key never reaches the page or the console. The server listens only on
127.0.0.1 and refuses requests that did not come from its own page, so another site
open in the browser cannot spend the key.

Needs Pillow (thumbnails, test images). The "2x" button runs tools/postprocess.py.
"""
import argparse
import base64
import io
import json
import os
import re
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
KEY_VAR = "OpenAI_CatGirl_API"
API = "https://api.openai.com/v1/images"
MODELS = {"gpt-image-2.6-sunburst": "Sunburst", "gpt-image-2.6-flare": "Flare"}
QUALITIES = ("medium", "high", "xhigh")
SIZES = {"portrait": "1024x1536", "mascot": "1024x1536", "landscape": "1536x1024", "square": "1024x1024",
         "wide": "2560x1088", "fhd": "1920x1088", "vertical": "1088x1920"}
# The API wants both sides in multiples of 16, so monitor sizes are requested 8 px larger
# and the saved file is cropped to the exact screen size.
CROP = {"wide": (2560, 1080), "fhd": (1920, 1080), "vertical": (1080, 1920)}
# USD per token and output tokens per image, from OpenAI's image generation guide and its
# cost calculator for GPT-Image-2.6 (checked 2026-11-17). Only used for estimates: the cost
# saved with each image comes from the usage the API reports.
PRICES = {"output": 30e-6, "textIn": 5e-6, "imageIn": 8e-6}
OUT_TOKENS = {
    "medium 1024x1536": 1132, "high 1024x1536": 1372, "xhigh 1024x1536": 2459,
    "medium 1536x1024": 1132, "high 1536x1024": 1372, "xhigh 1536x1024": 2459,
    "medium 1024x1024": 1449, "high 1024x1024": 1756, "xhigh 1024x1024": 3122,
    "medium 2560x1088": 950, "high 2560x1088": 1149, "xhigh 2560x1088": 2068,
    "medium 1920x1088": 1135, "high 1920x1088": 1325, "xhigh 1920x1088": 2356,
    "medium 1088x1920": 1135, "high 1088x1920": 1325, "xhigh 1088x1920": 2356,
}
STATIC = {"/app.js", "/engine.js", "/studio.js", "/data/catalog.js", "/data/prefabs.js"}
ID = re.compile(r"^\d{8}-\d{6}-[\w-]{1,60}$")


class StudioError(Exception):
    def __init__(self, message, status=502):
        super().__init__(message)
        self.status = status


def read_key():
    key = os.environ.get(KEY_VAR)
    if not key and sys.platform == "win32":
        import winreg
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as k:
                key = winreg.QueryValueEx(k, KEY_VAR)[0]
        except OSError:
            key = None
    return (key or "").strip() or None


def cost_of(usage):
    if not usage:
        return None
    details = usage.get("input_tokens_details") or {}
    text_in = details.get("text_tokens", 0 if details else usage.get("input_tokens", 0))
    image_in = details.get("image_tokens", 0)
    usd = usage.get("output_tokens", 0) * PRICES["output"] + text_in * PRICES["textIn"] + image_in * PRICES["imageIn"]
    return round(usd, 5)


def center_crop(png, target):
    """Crops the PNG to target (w, h) around its center; leaves it alone if it is not larger."""
    with Image.open(io.BytesIO(png)) as im:
        w, h = target
        left, top = (im.width - w) // 2, (im.height - h) // 2
        if im.size == (w, h) or left < 0 or top < 0:
            return png
        buf = io.BytesIO()
        im.crop((left, top, left + w, top + h)).save(buf, "PNG")
        return buf.getvalue()


def explain(status, err):
    """Turns an OpenAI error into a message for the page (the API's own text is kept)."""
    message = (err.get("message") or "").strip()
    if err.get("code") == "moderation_blocked":
        details = err.get("moderation_details") or {}
        stage = {"input": "no pedido", "output": "na imagem gerada"}.get(details.get("moderation_stage"), "")
        cats = ", ".join(details.get("categories") or [])
        return f"A OpenAI bloqueou {stage} pelo filtro de conteúdo{' (' + cats + ')' if cats else ''}. Mude o prompt e tente de novo."
    if status == 401:
        return f"A OpenAI recusou a chave em {KEY_VAR}. Confira se ela está certa e ativa."
    if status == 403:
        return f"A OpenAI negou o acesso: {message} (o modelo pode exigir verificar a organização no painel da OpenAI)."
    if status == 429:
        return f"Limite de uso ou de crédito atingido: {message}"
    return f"Erro {status} da OpenAI: {message or 'sem detalhes'}"


class OpenAIClient:
    def post(self, endpoint, params):
        key = read_key()
        if not key:
            raise StudioError(f"Não achei a chave: crie a variável de ambiente {KEY_VAR} com a sua chave da OpenAI.", 400)
        req = urllib.request.Request(
            f"{API}/{endpoint}", data=json.dumps(params).encode("utf-8"), method="POST",
            headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=330) as r:
                return json.load(r), r.headers.get("x-request-id")
        except urllib.error.HTTPError as e:
            try:
                err = json.load(e).get("error") or {}
            except (ValueError, AttributeError):
                err = {}
            raise StudioError(explain(e.code, err), 400 if err.get("type") == "image_generation_user_error" else 502)
        except urllib.error.URLError as e:
            raise StudioError(f"Sem conexão com a OpenAI: {e.reason}")
        except TimeoutError:
            raise StudioError("A OpenAI demorou mais de 5 minutos para responder.")


class FakeClient:
    """Answers like the API with drawn images, so the page can be tested without spending."""

    def post(self, endpoint, params):
        time.sleep(1.2)
        w, h = map(int, params["size"].split("x"))
        transparent = params.get("background") == "transparent"
        out = []
        for i in range(params.get("n", 1)):
            if endpoint == "edits":
                b64 = params["images"][0]["image_url"].split(",", 1)[1]
                im = Image.open(io.BytesIO(base64.b64decode(b64))).convert("RGBA")
            else:
                im = Image.new("RGBA", (w, h), (0, 0, 0, 0) if transparent else (238, 232, 244, 255))
                d = ImageDraw.Draw(im)
                d.ellipse((w * .3, h * .08, w * .7, h * .38), fill=(244, 214, 170, 255))
                d.rounded_rectangle((w * .22, h * .36, w * .78, h * .95), radius=60, fill=(201, 58, 100, 255))
            d = ImageDraw.Draw(im)
            label = "EDIT: " if endpoint == "edits" else f"FAKE {i + 1}  "
            text = label + params["prompt"][:150]
            band = h * .80 if endpoint == "edits" else h * .40
            d.rectangle((0, band, w, band + 120), fill=(20, 20, 30, 230))
            for j in range(0, min(len(text), 300), 50):
                d.text((20, band + 10 + j // 50 * 22), text[j:j + 50], fill=(255, 255, 255, 255), font_size=20)
            buf = io.BytesIO()
            im.save(buf, "PNG")
            out.append({"b64_json": base64.b64encode(buf.getvalue()).decode()})
        usage = {"input_tokens": 600, "output_tokens": OUT_TOKENS.get(f"{params['quality']} {params['size']}", 1400) * len(out),
                 "input_tokens_details": {"text_tokens": 500, "image_tokens": 1000 if endpoint == "edits" else 0}}
        return {"data": out, "usage": usage}, "fake"


class Store:
    def __init__(self, folder):
        self.dir = folder
        (folder / "thumbs").mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock()

    def new_id(self, prefab):
        base = datetime.now().strftime("%Y%m%d-%H%M%S-") + (re.sub(r"[^\w-]", "", prefab or "") or "img")[:40]
        with self.lock:
            name, i = base, 2
            while (self.dir / f"{name}.json").exists():
                name, i = f"{base}-{i}", i + 1
            (self.dir / f"{name}.json").write_text("{}", encoding="utf-8")  # reserve the name
        return name

    def save(self, png, meta):
        meta["id"] = image_id = self.new_id(meta.get("selection", {}).get("prefabId"))
        meta["file"] = f"{image_id}.png"
        (self.dir / meta["file"]).write_bytes(png)
        self.thumb(image_id)
        (self.dir / f"{image_id}.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
        return self.public(meta)

    def get(self, image_id):
        if not isinstance(image_id, str) or not ID.match(image_id):
            raise StudioError("Imagem inválida.", 400)
        try:
            meta = json.loads((self.dir / f"{image_id}.json").read_text(encoding="utf-8"))
        except (OSError, ValueError):
            raise StudioError("Essa imagem não existe mais na pasta.", 404)
        return meta, self.dir / meta["file"]

    def thumb(self, image_id):
        path = self.dir / "thumbs" / f"{image_id}.webp"
        if not path.exists():
            with Image.open(self.dir / f"{image_id}.png") as im:
                im.thumbnail((320, 320))
                im.save(path, "WEBP", quality=85)
        return path

    def public(self, meta):
        two_x = self.dir / "2x" / meta["file"]
        keep = ("id", "kind", "created", "model", "quality", "size", "savedSize", "background", "costUsd", "parent", "selection", "prompt")
        return {**{k: meta.get(k) for k in keep}, "url": f"/img/{meta['file']}", "thumb": f"/thumb/{meta['id']}.webp",
                "upscaled": f"/img/2x/{meta['file']}" if two_x.exists() else None}

    def recent(self, limit):
        items = []
        for path in sorted(self.dir.glob("*.json"), reverse=True):
            try:
                meta = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                continue
            if meta.get("file") and (self.dir / meta["file"]).exists():
                items.append(self.public(meta))
            if len(items) >= limit:
                break
        return items


def text_field(body, name, limit=32000):
    value = body.get(name)
    if not isinstance(value, str) or not value.strip() or len(value) > limit:
        raise StudioError(f"Campo '{name}' vazio ou grande demais.", 400)
    return value


def choice(body, name, allowed, default):
    value = body.get(name, default)
    if value not in allowed:
        raise StudioError(f"Valor inválido para '{name}'.", 400)
    return value


class Handler(BaseHTTPRequestHandler):
    server_version = "CatgirlStudio/1"

    def log_message(self, fmt, *args):
        sys.stderr.write(f"{self.command} {self.path.split('?')[0]} {args[1] if len(args) > 1 else ''}\n")

    # ------------------------------------------------------------ plumbing
    def send(self, status, body, ctype):
        self.send_response(status)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(body)

    def send_json(self, obj, status=200):
        self.send(status, json.dumps(obj, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8")

    def host_ok(self):
        return (self.headers.get("Host") or "").lower() in self.server.hosts

    def send_file(self, path, ctype):
        try:
            self.send(200, path.read_bytes(), ctype)
        except OSError:
            self.send_json({"error": "Arquivo não encontrado."}, 404)

    # --------------------------------------------------------------- GET
    def do_GET(self):
        if not self.host_ok():
            return self.send_json({"error": "Host não permitido."}, 403)
        path = self.path.split("?")[0]
        store = self.server.store
        if path in ("/", "/index.html"):
            return self.send(200, page_html().encode("utf-8"), "text/html; charset=utf-8")
        if path in STATIC:
            return self.send_file(ROOT / path.lstrip("/"), "text/javascript; charset=utf-8")
        if path == "/api/status":
            return self.send_json({
                "key": self.server.fake or bool(read_key()), "fake": self.server.fake, "outDir": str(store.dir),
                "models": MODELS, "qualities": QUALITIES, "sizes": SIZES, "outTokens": OUT_TOKENS,
                "prices": PRICES, "spent": self.server.spent})
        if path == "/api/gallery":
            return self.send_json({"images": store.recent(60)})
        m = re.match(r"^/img/(2x/)?([\w-]+\.png)$", path)
        if m:
            return self.send_file(store.dir / (m.group(1) or "") / m.group(2), "image/png")
        m = re.match(r"^/thumb/([\w-]+)\.webp$", path)
        if m:
            try:
                return self.send_file(store.thumb(store.get(m.group(1))[0]["id"]), "image/webp")
            except (StudioError, OSError):
                return self.send_json({"error": "Miniatura indisponível."}, 404)
        self.send_json({"error": "Não encontrado."}, 404)

    # -------------------------------------------------------------- POST
    def do_POST(self):
        origin = (self.headers.get("Origin") or "").lower()
        if not self.host_ok() or (origin and origin not in {f"http://{h}" for h in self.server.hosts}) \
                or self.headers.get("X-Studio") != "1":
            return self.send_json({"error": "Pedido recusado: só a página do estúdio pode usar estas rotas."}, 403)
        try:
            length = int(self.headers.get("Content-Length") or 0)
            if length > 1_000_000:
                raise StudioError("Pedido grande demais.", 413)
            body = json.loads(self.rfile.read(length) or b"{}")
            if not isinstance(body, dict):
                raise StudioError("Pedido inválido.", 400)
            route = {"/api/generate": self.generate, "/api/edit": self.edit, "/api/upscale": self.upscale}.get(self.path)
            if not route:
                raise StudioError("Não encontrado.", 404)
            self.send_json(route(body))
        except StudioError as e:
            self.send_json({"error": str(e)}, e.status)
        except ValueError:
            self.send_json({"error": "Pedido inválido."}, 400)

    def call(self, endpoint, params, kind, selection, parent=None, crop=None):
        result, request_id = self.server.client.post(endpoint, params)
        data = result.get("data") or []
        if not data:
            raise StudioError("A OpenAI respondeu sem imagem.")
        total = cost_of(result.get("usage"))
        each = round(total / len(data), 5) if total is not None else None
        with self.server.lock:
            self.server.spent = round(self.server.spent + (total or 0), 5)
        images = []
        for item in data:
            meta = {"kind": kind, "created": datetime.now().isoformat(timespec="seconds"), "model": params["model"],
                    "quality": params["quality"], "size": params["size"], "background": params["background"],
                    "prompt": params["prompt"], "selection": selection, "parent": parent, "costUsd": each,
                    "usage": result.get("usage"), "requestId": request_id, "crop": list(crop) if crop else None}
            png = base64.b64decode(item["b64_json"])
            if crop:
                png = center_crop(png, crop)
            with Image.open(io.BytesIO(png)) as im:
                meta["savedSize"] = f"{im.width}x{im.height}"
            images.append(self.server.store.save(png, meta))
        return {"images": images, "costUsd": total, "spent": self.server.spent}

    def generate(self, body):
        selection = body.get("selection") if isinstance(body.get("selection"), dict) else {}
        n = body.get("n", 1)
        if n not in (1, 2, 3, 4):
            raise StudioError("Quantidade inválida.", 400)
        fmt = choice(body, "format", SIZES, "portrait")
        params = {
            "model": choice(body, "model", MODELS, "gpt-image-2.6-sunburst"),
            "prompt": text_field(body, "prompt"),
            "size": SIZES[fmt],
            "quality": choice(body, "quality", QUALITIES, "high"),
            "background": "transparent" if body.get("transparent") else "opaque",
            "output_format": "png", "n": n}
        return self.call("generations", params, "generate", selection, crop=CROP.get(fmt))

    def edit(self, body):
        meta, path = self.server.store.get(body.get("parent"))
        selection = body.get("selection") if isinstance(body.get("selection"), dict) else {}
        params = {
            "model": choice(body, "model", MODELS, meta.get("model")),
            "prompt": text_field(body, "prompt"),
            "size": meta["size"], "quality": choice(body, "quality", QUALITIES, meta.get("quality")),
            "background": meta.get("background", "auto"), "output_format": "png", "n": 1,
            "images": [{"image_url": "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode()}]}
        crop = meta.get("crop")
        return self.call("edits", params, "edit", selection, parent=meta["id"], crop=tuple(crop) if crop else None)

    def upscale(self, body):
        meta, path = self.server.store.get(body.get("id"))
        out = self.server.store.dir / "2x"
        r = subprocess.run([sys.executable, str(ROOT / "tools" / "postprocess.py"), str(path), "--out", str(out)],
                           capture_output=True, text=True, errors="replace", timeout=900)
        if not (out / path.name).exists():
            tail = "\n".join((r.stdout + r.stderr).strip().splitlines()[-4:])
            raise StudioError(f"O 2x não saiu: {tail}", 500)
        return {"upscaled": f"/img/2x/{path.name}"}


def page_html():
    """page.html is written for the artifact host, which adds the document shell; add it here."""
    body = (ROOT / "page.html").read_text(encoding="utf-8")
    return ('<!doctype html><html lang="pt-BR"><head><meta charset="utf-8">'
            '<meta name="viewport" content="width=device-width,initial-scale=1"></head><body>\n'
            + body + '\n<script src="studio.js"></script>\n</body></html>\n')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--port", type=int, default=8770)
    ap.add_argument("--out", type=Path, default=Path.home() / "Pictures" / "Catgirls" / "api",
                    help="where images are saved (default: Pictures\\Catgirls\\api)")
    ap.add_argument("--fake", action="store_true", help="draw test images instead of calling the API (costs nothing)")
    args = ap.parse_args()

    server = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    server.hosts = {f"127.0.0.1:{args.port}", f"localhost:{args.port}"}
    server.store = Store(args.out)
    server.fake = args.fake
    server.client = FakeClient() if args.fake else OpenAIClient()
    server.lock = threading.Lock()
    server.spent = 0.0
    key = "test mode, no API calls" if args.fake else ("found" if read_key() else f"MISSING: set {KEY_VAR}")
    print(f"Catgirl studio on http://127.0.0.1:{args.port}  |  key: {key}  |  images: {args.out}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
