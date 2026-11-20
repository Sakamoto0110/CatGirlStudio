# CatGirlStudio

CatGirlStudio is a Windows character studio and image creation pipeline. It
focuses first on original adult feminine characters, including human waifus
and cat-girls. Its OutfitEngine builds outfits from garments, layers, and
piece-specific options.

Start with a pose, customize the character and outfit, then review and
edit the English prompt. Copy it into another tool or generate and edit
images through the OpenAI Images API.

The current desktop catalog starts from a cat-girl template. Reusable NPC
identities are a key goal so one character can appear across a set of
expressions or outfits while keeping the same face, body, and personality.

The desktop interface is in Brazilian Portuguese. Prompt building, favorites,
and the image library are local. Generating or editing an image in the app sends
the prompt to OpenAI. Image edits also send the selected source image.

[Product direction](PRODUCT.md) · [Roadmap](ROADMAP.md) · [Catalog guide](docs/catalog.md)

## Build and run

Published Windows x64 packages are available on [GitHub Releases](https://github.com/Sakamoto0110/CatGirlStudio/releases).
Extract the ZIP and run `CatgirlStudio.exe`. The `.sha256` file records the
SHA-256 checksum for the ZIP.

Build on Windows with the .NET 10 SDK. The published x64 executable requires the
.NET 10 Desktop Runtime and includes the catalogs and app icon.

```powershell
dotnet publish src/CatgirlStudio.App -c Release
.\publish\CatgirlStudio.exe
```

Close any running instance from the publish folder before rebuilding it. To try
the app with simulated local images and no API credentials, run:

```powershell
.\publish\CatgirlStudio.exe --fake
```

For live generation, configure a user environment variable named
`OpenAI_CatGirl_API` with your own API key and open the app. Keep the key out of
the repository, prompts, and screenshots. Without a key, the prompt editor and
copy action are still available.

## Working in the studio

1. Pick a pose and customize the character, outfit, background, and framing.
   Lock the fields you want to keep when switching poses or rolling new options.
2. Review the prompt. You can edit it directly before copying or generating.
   After a manual edit, later field changes leave your text in place and show a
   warning. **Voltar ao gerado** restores the generated prompt.
3. Click **Gerar imagem** when the prompt is ready. The button displays a cost
   estimate. The gallery saves a PNG and a JSON sidecar with the submitted prompt,
   selection, and generation details.
4. Select a recent image to restore its settings, including any hand-edited
   generation prompt. Change the fields and review **Ajuste rápido** before
   applying the edit to the selected image. Favorites save reusable setups.

Use **Roupa → Peças** to combine garments, underlayers, and accessories. Nightgowns
appear under **Parte de cima** and replace the separate bottom while selected.
Use **Outra cor…** on a supported color field to enter a custom hex color.
Drag the panel dividers to resize them, or double-click one to reset it.

By default, state and favorites live in `%AppData%\CatgirlStudio`, and images
live in the Windows Pictures folder under `Catgirls\api`. Wallpaper presets crop
to 2560×1080, 1920×1080, or 1080×1920 on save.

## Optional upscaling

The app can upscale PNGs by 2×, 3×, or 4× using the separate Vulkan build of
[Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.5.0).
Extract the Windows package into a `realesrgan\` folder beside the executable,
keeping its `models\` folder. In a development checkout, the app also looks in
`tools\bin\realesrgan-ncnn-vulkan\`.

Upscaling processes color and transparency separately before merging them. TTA
uses additional passes and takes longer. Results go in folders such as `2x` or
`4x-tta` beside the original images.

## Development

The planned architecture divides responsibilities among **Character** (the
reusable definition), **Composition** (the choices for an image), **Generation**
(prompt building and image processing), and **Assets** (the resulting images and
variants). See the [responsibility map](ROADMAP.md#planned-responsibilities).

| Path | Purpose |
| --- | --- |
| [src/CatgirlStudio.App](src/CatgirlStudio.App) | WPF workspace, local state, image library, and API client |
| [src/CatgirlStudio.Core](src/CatgirlStudio.Core) | Catalog, prompt and outfit engines, and image operations |
| [data](data) | Desktop catalogs for prompt options, poses, and outfits |
| [tools/CatalogCheck](tools/CatalogCheck) | Catalog validation and prompt inspection |
| [web](web) | Legacy browser prototype with its own frozen catalog |

```powershell
dotnet build CatgirlStudio.slnx -c Release
dotnet run --project tools/CatalogCheck
```

For a desktop smoke test, publish the app and run the following with disposable
folders for settings and images. The test writes its findings to the specified
report and creates screenshots beside it.

```powershell
$smoke = Join-Path ([System.IO.Path]::GetTempPath()) ("CatgirlStudio-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $smoke | Out-Null
Start-Process -FilePath .\publish\CatgirlStudio.exe -ArgumentList @(
    '--fake', '--data', ('"{0}"' -f (Join-Path $smoke 'state')),
    '--out', ('"{0}"' -f (Join-Path $smoke 'images')),
    '--selftest', ('"{0}"' -f (Join-Path $smoke 'report.txt'))
) -Wait
Get-Content (Join-Path $smoke 'report.txt')
```

| Option | Purpose |
| --- | --- |
| `--fake` | Use simulated images without sending requests or incurring charges. |
| `--data DIR` | Use a different directory for settings and favorites. |
| `--out DIR` | Use a different directory for images and their metadata. |
| `--accent "#RRGGBB"` | Preview another interface accent color. |
| `--screenshot FILE.png` | Capture the app window and exit. |
| `--fake --selftest FILE` | Exercise the main desktop workflow and write a report. |

## Releases

GitHub Actions builds the Windows app and checks the catalogs for pushes to
`main` and pull requests. A successful run provides a Windows x64 package.
Push a version tag such as `v0.1.0-preview.1` to publish the package and its
checksum as a GitHub Release. Tags with a suffix create prereleases.

The bundled Neko app icon comes from the project's shared vD-Resources asset
collection. Real-ESRGAN is a separate download.
