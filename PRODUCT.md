# Product direction

CatGirlStudio is a character studio and image creation pipeline. Its primary
focus is original adult feminine characters, including human waifus and
cat-girls. A detailed OutfitEngine makes the wardrobe a central part of
character design. The studio takes a character from editable prompt to
generated images and image variants.

## Who it serves

Start with creators who want to design a character once and use them across
portraits, wallpapers, mascots, or game NPC assets. Keep the studio usable for
one-off illustrations and for sets that need a recognizable character across
many images.

## The workflow

**Build a character → compose an image → review and edit → copy or generate → refine.**

- **Build and compose.** Assemble a character and an outfit. Choose an
  expression, pose, scenario, and framing for the image.
- **Review and edit.** Keep the prompt visible and editable at the point where
  the creator decides what to send. Make it clear when the fields have diverged
  from a hand-edited prompt.
- **Copy or generate.** Treat both routes as first-class. Prompt authoring works
  locally. Image generation and image edits use the configured provider after
  an explicit action.
- **Revisit and refine.** Keep the original prompt and selection with each
  generated image so the creator can recover a setup and make a targeted edit.
  Reuse favorites to start new scenes with a familiar character.

## NPC identity

Consistent NPC generation is a key product goal. Each NPC should have a saved
identity with a stable ID, visual traits, and a linked personality. The visual
identity includes the face, build, proportions, and other defining features.
A saved personality belongs to the character and can inform expressions and poses.

An image variant changes the expression, pose, or scenario and can optionally
change the outfit. Those changes should leave the face and body recognizable
as the same character. A generation batch uses the same identity across its
variants, and each output keeps its link to that identity.

## Architecture direction

Implement character types through shared interfaces in the core, with human
and cat-girl types as the first cases. The studio should work with the
capabilities a type exposes. Hair, clothing, pose, and scene are shared where
they apply. Feline ears and tails belong to types that support them.

Model body size, proportions, and feminine or masculine presentation
independently of character type. Keep reusable identity and personality
separate from image-specific options, and share the OutfitEngine across
compatible character types. Preserve saved IDs and restore older selections
when replacing the current fixed cat-girl prompt template with this interface.

## Product decisions

- **Keep the creative focus.** Default prompts describe adult characters in
  non-sexual scenes. Expand the wardrobe and hair catalog by composing cuts,
  materials, details, and compatible layers where they can vary independently.
- **Make changes easy to predict.** Offer properties that apply to the selected
  piece, preserve stable catalog identifiers, and keep the creator's editable
  prompt in control of the submitted text.
- **Make the desktop app the supported editing surface.** Use the Windows app
  and its shared core for new work. Keep the browser prototype as a historical
  reference with its existing catalog.
- **Keep work portable and understandable.** Store settings and image history
  locally in ordinary files. Favor visible costs and actionable errors for
  network operations. Keep credentials separate from saved work.

## Desktop baseline

The desktop app already supports building an outfit, editing and copying or
sending a prompt, and restoring a setup from a favorite or image. Its built-in
prompt currently starts from an adult cat-girl template. The
[roadmap](ROADMAP.md) tracks the work to generalize that baseline and support
the full character pipeline.
