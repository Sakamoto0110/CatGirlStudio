# CatGirlStudio roadmap

This roadmap lists planned work by topic. Implementation order and milestones
are open. **Consistent NPC generation** is a key product goal.

## Planned responsibilities

This is the intended division of responsibilities for CatGirlStudio.

```text
CatGirlStudio
├── Character
│   ├── Identity
│   ├── Anatomy
│   ├── Species
│   └── Personality
├── Composition
│   ├── OutfitEngine
│   ├── Appearance
│   ├── Pose
│   └── Scenario
├── Generation
│   ├── PromptBuilder
│   ├── BatchProcessor
│   └── ImageProcessor
└── Assets
    ├── NPC Variants
    ├── Expressions
    └── 2D Transformations
```

- **Character** owns the reusable character definition, including its identity,
  anatomy, species, and personality. Shared interfaces expose the
  capabilities of humans and cat-girls.
- **Composition** defines how a character appears in one image. It selects
  clothing through the OutfitEngine and combines presentation choices, pose,
  and scenario with the saved character definition.
- **Generation** builds the editable prompt from the character and composition,
  runs image jobs, and processes the results. The ImageProcessor performs
  pixel operations such as crop and rotation.
- **Assets** organizes the outputs, including NPC variants, expression sets,
  and transformed copies. Each variant keeps a link to its character, and a
  transformed copy keeps a link to its source image.

## Current baseline

- A WPF desktop app with a cat-girl prompt template, pose and character
  controls, a modular OutfitEngine, and editable English prompts.
- Prompt copying, OpenAI image generation and edits, local history with JSON
  sidecars, and favorites. One generation request can return several images
  from the same prompt.
- Transparent PNG and wallpaper workflows, plus optional Real-ESRGAN upscaling.
- A catalog checker and a simulated desktop smoke test.

## Image batches

Queue multiple image jobs in one run. A batch can repeat one setup or contain
distinct expressions, outfits, poses, or scenarios for the same character.
Preview the variants and estimated total cost before submitting. Show each
job's status and keep completed outputs available when another job fails.

**Done when** a creator can queue an NPC expression set, inspect the result of
each variant, and retry a failed item on its own.

## Wardrobe, hair, and scenarios

- Expand the wardrobe substantially. Split configurable elements into reusable
  silhouettes, fabrics, lengths, trims, colors, patterns, and layers where
  they vary independently. Keep compatibility rules in the OutfitEngine.
- Extend the existing build-by-parts hair editor with more cuts, lengths,
  textures, fringes, and styling details. Keep ready-made looks as shortcuts.
- Add more scenario presets, such as interiors, outdoor locations, and
  everyday settings. Reuse a character across them with the same identity.
- Make large catalogs easier to browse through search and grouping.

## Body controls and masculine characters

Add direct control over body size, build, and proportions. Save these as part
of an NPC's identity and carry them into image variants. Expand the supported
appearances to masculine characters through the shared character interfaces,
with applicable hair, wardrobe, and prompt options.

## Consistent NPC generation

- Define a shared character interface and add human and cat-girl implementations.
  Load older saved selections through the cat-girl implementation.
- Save a reusable NPC identity with defining facial and body traits, body size
  and proportions, and a linked personality. Store an expression or wardrobe
  change as a variant of that identity.
- Evaluate reference images and targeted edits for visual consistency. Review
  generated variants side by side and flag drift in the face, silhouette, or
  proportions before accepting the set.

**Done when** a creator can reopen an NPC with its personality and generate
a neutral portrait, several expressions, and an alternate outfit. The set is
recognizable as the same character with the same facial features and body
proportions, and each image points back to the NPC.

## 2D transformations

Add a post-processing step for generated assets. Candidate first operations
are flip, rotation, scale, and crop. Save transformed copies beside the source
and preserve transparency and the connection to the original image.

## Release and library work

Extend release qualification with a desktop smoke test and improve image API
diagnostics. Review project licensing separately from the Windows preview.
Group gallery images by character and batch as those features become available.
