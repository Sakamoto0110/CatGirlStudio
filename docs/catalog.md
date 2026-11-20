# Catalog guide

The desktop app embeds three files at publish time. Run the catalog checker
and republish after editing them. The copies in `web/data` belong to the legacy
browser prototype.

| File | Contents |
| --- | --- |
| [data/catalog.json](../data/catalog.json) | Prompt blocks, character and scene variables, shared colors, and patterns |
| [data/prefabs.json](../data/prefabs.json) | Pose cards, pose text, and optional presets |
| [data/outfits.json](../data/outfits.json) | Outfit modes, garment slots, piece properties, and outfit phrasing |

## Prompt templates

The [prompt engine](../src/CatgirlStudio.Core/PromptEngine.cs) starts at
`blocks.prompt` and expands the pose, blocks, outfit provider, and selected
options. Variables reached during expansion determine which fields appear in
the editor.

- `{{name}}` expands a block, provider value, or selected option.
- `{{var.field}}` reads an auxiliary field from the selected option. Each
  option of that variable needs the field.
- `{{list:a,b,c}}` drops empty entries and joins the rest. Keep each list item
  free of commas so the join stays readable.
- Multi-select options with the same `slot` replace one another. A theme can
  introduce a detail field and make matching outfit pieces available.
- Empty option text removes adjacent joiners where applicable, such as
  `with {neckwear}` when no neckwear is selected.

Keep option IDs stable because favorites and image metadata store them.
Use `pt` for interface labels and `en` for prompt phrases.

## Outfit pieces

The [outfit engine](../src/CatgirlStudio.Core/OutfitEngine.cs) uses a mode to
decide which slots appear. Within each slot, a garment lists its selectable
properties and uses `{property}` in its English phrase.

- A piece inherits slot properties. It can override a `default`, limit options
  with `allow`, lock a choice with `fixed`, or remove a property with `null`.
- A default such as `"@accentColor"` follows the named catalog variable.
  Supported custom colors render as an English color name and the hex value.
- `covers` suppresses other garment slots. A nightgown can cover `bottom`,
  and the previous bottom becomes visible again when the nightgown is replaced.
  `legs` controls whether legwear is offered for the active silhouette.
- `theme` limits a piece to the matching style. `relaxed` selects the relaxed
  text for `{{outfitModesty}}`.
- In Pieces mode, selecting no top uses the basic cotton bra if no bra is
  specified. An explicitly selected bra is preserved.

To add a nightgown for **Peças → Parte de cima**, add an entry to
`slots.top.pieces` in `data/outfits.json`. The following existing entry shows the
shape. Choose a new `id` and adapt the label and wording for a new piece.

```json
{
  "id": "nightgownjersey",
  "pt": "Camisola de malha macia",
  "relaxed": true,
  "legs": true,
  "covers": ["bottom"],
  "en": "a loose {color} {pattern} jersey nightgown with short sleeves"
}
```

`id`, `pt`, and `en` are required. This example inherits `color` and `pattern`
from the top slot. To add an entire outfit that appears in **Pronta**, put the
entry in `slots.full.pieces` and use that slot's properties such as `{main}`
and `{accent}`.

After checking and republishing, launch with `--fake`, select the new item in
its mode, and check that its English wording and customizations appear in the
prompt. For a piece that uses `covers`, verify that the covered field disappears
and returns when you switch back.

## Pose presets

Add an entry to `prefabs` with a stable `id`, interface name and description,
a category from the catalog, English pose text, and any applicable defaults.
Use `"outfit": "<id>"` to select a ready outfit. Leave character defaults out
of a pose that should keep the current character intact.

## Check changes

```powershell
dotnet run --project tools/CatalogCheck
```

The checker validates references, option and property combinations, formatted
prompt output, layering, and custom color support. Use `-- --show` for a sample
prompt, `-- --colors` to inspect color names, or `-- --compare-web` to compare
with the frozen browser catalog.
