"""Cleans the transparency of GPT images and upscales them 2x for wallpapers.

The GPT delivers 1024x1536 PNGs with "dirty" transparency: the background keeps
an alpha of 1-2/255 and the character never goes above 254. Cleanup maps
alpha 4..249 onto 0..255. The upscale uses Real-ESRGAN ncnn-vulkan with
realesr-animevideov3 at 2x (realesrgan-x4plus-anime smears fine textures such
as polka dots and prints). Colour and alpha are upscaled separately, with the
colour first spread under the transparent pixels so the edges get no halo.

    python tools/postprocess.py image.png                 # writes 2x/image.png
    python tools/postprocess.py folder                    # every image in it
    python tools/postprocess.py image.png --out wallpapers
    python tools/postprocess.py image.png --no-upscale    # only cleans: limpo/image.png

Alpha cleanup runs only when the image looks like raw GPT output (max alpha
below 255); --clean always / never overrides that. Existing outputs are kept
unless --force. Needs Pillow and the Real-ESRGAN exe in
tools/bin/realesrgan-ncnn-vulkan/ (see --esrgan).
"""
import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageChops, ImageStat

HERE = Path(__file__).resolve().parent
DEFAULT_ESRGAN = HERE / "bin" / "realesrgan-ncnn-vulkan" / "realesrgan-ncnn-vulkan.exe"
DOWNLOAD_URL = ("https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/"
                "realesrgan-ncnn-vulkan-20220424-windows.zip")
MODEL = "realesr-animevideov3"
ALPHA_LOW, ALPHA_HIGH = 4, 249     # raw GPT alpha: residue below, solid above
SNAP_LOW, SNAP_HIGH = 5, 252       # upscaled alpha: ringing at both ends
EXTENSIONS = {".png", ".webp", ".jpg", ".jpeg"}


def clean_alpha(alpha):
    """alpha' = clamp((a - 4) / (249 - 4)): drops the background residue, makes the character solid."""
    span = ALPHA_HIGH - ALPHA_LOW
    return alpha.point([min(255, max(0, round((v - ALPHA_LOW) * 255 / span))) for v in range(256)])


def snap_alpha(alpha):
    """The upscaler leaves faint ringing (1..5) in the background and 252..254 inside the
    character; snaps those to 0 and 255 and leaves the soft edge in between alone."""
    return alpha.point([0 if v <= SNAP_LOW else 255 if v >= SNAP_HIGH else v for v in range(256)])


def bleed(rgb, alpha):
    """Fills the colour under fully transparent pixels from the visible ones (pull-push).

    Without it the upscaler blends whatever junk sits under alpha=0 into the edges.
    Visible pixels are returned unchanged.
    """
    known = alpha.point(lambda v: 255 if v else 0)
    if known.getextrema() != (0, 255):  # nothing visible, or nothing transparent
        return rgb
    levels = [(ImageChops.multiply(rgb, known.convert("RGB")), known)]
    while max(levels[-1][1].size) > 1:
        colour, weight = levels[-1]
        levels.append((colour.reduce(2), weight.reduce(2)))
    mean = tuple(round(c) for c in ImageStat.Stat(rgb, known).mean)
    fill = Image.new("RGB", levels[-1][1].size, mean)
    for colour, weight in reversed(levels):
        fill = fill.resize(colour.size, Image.BILINEAR)
        fill = ImageChops.add(colour, ImageChops.multiply(fill, ImageChops.invert(weight).convert("RGB")))
    return fill


def load(path, clean):
    """Returns (rgb, alpha or None, icc profile, whether the alpha was cleaned, note)."""
    with Image.open(path) as im:
        icc = im.info.get("icc_profile")
        has_alpha = im.mode in ("RGBA", "LA", "PA") or "transparency" in im.info
        im = im.convert("RGBA" if has_alpha else "RGB")
    if not has_alpha:
        return im, None, icc, False, "no transparency"
    alpha = im.getchannel("A")
    low, high = alpha.getextrema()
    if low == 255:
        return im.convert("RGB"), None, icc, False, "fully opaque"
    if clean == "always" or (clean == "auto" and high < 255):
        return im.convert("RGB"), clean_alpha(alpha), icc, True, f"alpha cleaned (max was {high})"
    return im.convert("RGB"), alpha, icc, False, "alpha already clean"


def collect(inputs):
    files = []
    for p in map(Path, inputs):
        if p.is_dir():
            files += sorted(f for f in p.iterdir() if f.is_file() and f.suffix.lower() in EXTENSIONS)
        elif p.is_file():
            files.append(p)
        else:
            sys.exit(f"not found: {p}")
    return files


def save(rgb, alpha, icc, dest):
    if alpha is not None:
        rgb = rgb.copy()
        rgb.putalpha(alpha)
    dest.parent.mkdir(parents=True, exist_ok=True)
    rgb.save(dest, icc_profile=icc) if icc else rgb.save(dest)


def run_esrgan(exe, src_dir, dst_dir):
    cmd = [str(exe), "-i", str(src_dir), "-o", str(dst_dir), "-n", MODEL, "-s", "2",
           "-f", "png", "-m", str(exe.parent / "models")]
    result = subprocess.run(cmd, capture_output=True, text=True, errors="replace")
    missing = [f.name for f in src_dir.iterdir() if not (dst_dir / f.name).exists()]
    if result.returncode or missing:
        tail = "\n".join((result.stderr or result.stdout).strip().splitlines()[-15:])
        sys.exit(f"Real-ESRGAN failed (exit {result.returncode}, missing {len(missing)} outputs):\n{tail}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("inputs", nargs="+", help="images or folders of images")
    ap.add_argument("--out", type=Path, help="output folder (default: '2x' or 'limpo' next to each input)")
    ap.add_argument("--no-upscale", action="store_true", help="only clean the alpha")
    ap.add_argument("--clean", choices=("auto", "always", "never"), default="auto",
                    help="alpha cleanup (default auto: only when max alpha < 255)")
    ap.add_argument("--force", action="store_true", help="overwrite existing outputs")
    ap.add_argument("--esrgan", type=Path, default=DEFAULT_ESRGAN, help="path to realesrgan-ncnn-vulkan.exe")
    args = ap.parse_args()

    if not args.no_upscale and not args.esrgan.is_file():
        sys.exit(f"Real-ESRGAN not found at {args.esrgan}\n"
                 f"Download {DOWNLOAD_URL}\nand extract it there (or pass --esrgan).")

    jobs = []
    for src in collect(args.inputs):
        dest = (args.out or src.parent / ("limpo" if args.no_upscale else "2x")) / (src.stem + ".png")
        if dest.resolve() == src.resolve():
            sys.exit(f"output would overwrite the input: {src}")
        if dest.exists() and not args.force:
            print(f"skip   {src.name}: {dest} already exists (--force to redo)")
            continue
        jobs.append((src, dest))
    if not jobs:
        return

    with tempfile.TemporaryDirectory(prefix="catgirl-pp-") as tmp:
        stage, upscaled = Path(tmp, "in"), Path(tmp, "out")
        stage.mkdir()
        upscaled.mkdir()
        staged = []
        for i, (src, dest) in enumerate(jobs):
            rgb, alpha, icc, cleaned, note = load(src, args.clean)
            if args.no_upscale:
                if not cleaned:
                    print(f"skip   {src.name}: {note}, nothing to clean")
                    continue
                save(rgb, alpha, icc, dest)
                print(f"clean  {src.name}: {note} -> {dest}")
                continue
            if alpha is None:
                rgb.save(stage / f"{i:04d}-rgb.png")
            else:
                bleed(rgb, alpha).save(stage / f"{i:04d}-rgb.png")
                alpha.convert("RGB").save(stage / f"{i:04d}-alpha.png")
            staged.append((i, src, dest, icc, note, alpha is not None))
        if not staged:
            return

        print(f"upscaling {len(staged)} image(s) with {MODEL} x2...", flush=True)
        run_esrgan(args.esrgan, stage, upscaled)

        for i, src, dest, icc, note, has_alpha in staged:
            with Image.open(upscaled / f"{i:04d}-rgb.png") as im:
                rgb = im.convert("RGB")
            alpha = None
            if has_alpha:
                with Image.open(upscaled / f"{i:04d}-alpha.png") as im:
                    alpha = snap_alpha(im.convert("L"))
            save(rgb, alpha, icc, dest)
            print(f"2x     {src.name}: {note}, {rgb.width}x{rgb.height} -> {dest}")


if __name__ == "__main__":
    main()
