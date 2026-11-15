/*
 * Catgirl Prompt Builder: variable catalog and shared prompt blocks.
 *
 * How a prompt is assembled
 * -------------------------
 * Text can contain {{name}} placeholders. A placeholder resolves, in order, to:
 *   1. {{pose}}            -> the selected prefab's pose text (data/prefabs.js)
 *   2. a block             -> CATALOG.blocks[name], expanded again
 *   3. a variable          -> the text ("en") of the option chosen for CATALOG.vars[name]
 *                             (option text may itself contain placeholders, e.g. outfits
 *                             use {{mainColor}}, {{accentColor}} and {{legwear}})
 * The whole prompt starts from CATALOG.blocks.prompt.
 *
 * {{list:a,b,c}} expands each name the same way, drops the ones that come out empty
 * (a "none" option) and joins the rest as "x, y and z". Text that goes into a list
 * should have no commas of its own, or the list stops reading as a list.
 *
 * An option with en "" says nothing. A joining word left before it ("shorts and
 * {{legwear}}") is removed when that happens.
 *
 * Only the variables a prompt actually reaches are shown in the UI, so an outfit
 * without socks will not show the "legwear" field.
 *
 * Option fields
 *   id  stable key (stored in favourites, do not rename once used)
 *   pt  label shown in the UI (Portuguese)
 *   en  text that goes into the prompt (English, it is what the image model reads)
 *
 * Variable fields
 *   label    UI label (Portuguese)
 *   name     English name, used in the "change only..." edit request
 *   group    UI section
 *   default  option id used when nothing else sets it
 */
window.CATALOG = {
  groups: ["Imagem", "Personagem", "Roupa", "Clima e expressão", "Câmera e estilo"],

  blocks: {
    prompt: [
      "{{format}} {{background}}",
      "",
      "{{character}} {{outfitSentence}} {{hairAccessory}} {{accessory}}",
      "",
      "{{vibe}} {{modesty}}",
      "",
      "Focus: {{focus}}",
      "",
      "Pose: {{pose}}.",
      "",
      "Expression: {{expression}}, {{gaze}}.",
      "",
      "Camera: {{camera}}.",
      "",
      "Style: {{style}} Color: {{palette}}.",
      "",
      "{{rules}}"
    ].join("\n"),

    character:
      "An adult cat-girl with {{hairColor}} hair in {{hairStyle}}, {{eyeColor}} eyes, {{ears}}, and {{tail}}.",

    outfitSentence: "She wears {{outfit}}.",

    // House rules learned while testing: GPT drifts to "sensual" unless told not to,
    // reuses earlier images unless told not to, and needs "adult" to avoid refusals.
    modesty:
      "Modest, non-revealing clothing: no cleavage, no bare shoulders, no emphasis on the body.",

    rules:
      "Create a completely new, original image; do not reuse or copy any previous image. " +
      "Clean anatomy with natural hands. No text, no watermark, no signature."
  },

  vars: {
    /* ---------------------------------------------------------------- Imagem */
    format: {
      label: "Formato", name: "image format", group: "Imagem", default: "portrait",
      options: [
        { id: "portrait", pt: "Retrato 2:3 (só a personagem)", en: "Create a vertical anime illustration, 1024x1536." },
        { id: "landscape", pt: "Paisagem 3:2 (wallpaper)", en: "Create a horizontal anime illustration, 1536x1024, composed as a desktop wallpaper: the character sits in the right third of the frame and the left two-thirds stay calm and mostly empty, with room for desktop icons." },
        { id: "square", pt: "Quadrado 1:1 (avatar)", en: "Create a square anime illustration, 1024x1024." }
      ]
    },

    background: {
      label: "Fundo", name: "background", group: "Imagem", default: "transparent",
      options: [
        { id: "transparent", pt: "Transparente (PNG)", en: "Fully transparent background (PNG), no floor shadow." },
        { id: "white", pt: "Branco liso (fácil de recortar)", en: "Plain solid white background, no shadow, no other elements." },
        { id: "tint", pt: "Cor suave lisa (tom da cor de destaque)", en: "Plain soft background in a very light tint of {{accentColor}}, no other elements." },
        { id: "bedroom", pt: "Quarto aconchegante", en: "Background: a cozy bedroom with fairy lights and plush pillows, softly blurred and low-detail so the character stands out." },
        { id: "cafe", pt: "Café com janela chuvosa", en: "Background: a quiet café by a rainy window, warm lamps inside, softly blurred and low-detail so the character stands out." },
        { id: "snow", pt: "Rua com neve à noite", en: "Background: a snowy street at night with warm shop lights and gently falling snow, softly blurred and low-detail so the character stands out." },
        { id: "rooftop", pt: "Telhado ao pôr do sol", en: "Background: a rooftop at sunset with a distant city skyline, softly blurred and low-detail so the character stands out." },
        { id: "library", pt: "Biblioteca", en: "Background: a warm library with tall wooden bookshelves, softly blurred and low-detail so the character stands out." },
        { id: "sakura", pt: "Parque de cerejeiras", en: "Background: a park with cherry blossom trees and drifting petals, softly blurred and low-detail so the character stands out." },
        { id: "window", pt: "Janela com chuva", en: "Background: a wide window with rain streaks on the glass and a grey-blue city behind it, softly blurred and low-detail so the character stands out." }
      ]
    },

    /* ------------------------------------------------------------ Personagem */
    hairColor: {
      label: "Cor do cabelo", name: "hair color", group: "Personagem", default: "honey",
      options: [
        { id: "platinum", pt: "Loiro platinado", en: "platinum blonde" },
        { id: "honey", pt: "Loiro mel", en: "soft honey blonde" },
        { id: "golden", pt: "Loiro dourado", en: "bright golden blonde" },
        { id: "ash", pt: "Cinza (ash)", en: "ash gray" },
        { id: "silver", pt: "Prateado", en: "silver" },
        { id: "lavender", pt: "Lavanda prateado", en: "lavender-silver" },
        { id: "white", pt: "Branco", en: "snow white" },
        { id: "black", pt: "Preto", en: "glossy black" },
        { id: "darkbrown", pt: "Castanho escuro", en: "dark brown" },
        { id: "chestnut", pt: "Castanho claro", en: "warm chestnut brown" },
        { id: "auburn", pt: "Ruivo acastanhado", en: "auburn" },
        { id: "ginger", pt: "Ruivo alaranjado", en: "ginger orange" },
        { id: "pink", pt: "Rosa pastel", en: "pastel pink" },
        { id: "cherry", pt: "Rosa cereja", en: "cherry pink" },
        { id: "skyblue", pt: "Azul claro", en: "light sky blue" },
        { id: "navy", pt: "Azul marinho", en: "deep navy blue" },
        { id: "mint", pt: "Verde menta", en: "mint green" },
        { id: "twotone", pt: "Bicolor preto e branco", en: "two-tone black and white" }
      ]
    },

    hairStyle: {
      label: "Penteado", name: "hairstyle", group: "Personagem", default: "twintails",
      options: [
        { id: "bob", pt: "Chanel curto", en: "a short, soft bob" },
        { id: "twintails", pt: "Maria-chiquinha curta", en: "short, fluffy twin tails" },
        { id: "longtwintails", pt: "Maria-chiquinha longa", en: "long twin tails" },
        { id: "straight", pt: "Longo e liso", en: "a long, straight style" },
        { id: "wavy", pt: "Muito longo e ondulado", en: "a very long, wavy style" },
        { id: "sideponytail", pt: "Rabo de cavalo baixo de lado", en: "a low side ponytail" },
        { id: "ponytail", pt: "Rabo de cavalo alto", en: "a high ponytail" },
        { id: "braids", pt: "Duas tranças soltas", en: "two loose braids" },
        { id: "messy", pt: "Médio bagunçado", en: "a messy medium-length style" },
        { id: "hime", pt: "Hime (franja reta)", en: "a hime cut with straight bangs" },
        { id: "halfup", pt: "Meio preso com coque", en: "a half-up style with a small bun" },
        { id: "wolf", pt: "Wolf cut", en: "a fluffy wolf cut" }
      ]
    },

    eyeColor: {
      label: "Olhos", name: "eye color", group: "Personagem", default: "blue",
      options: [
        { id: "blue", pt: "Azul vivo", en: "big, bright blue" },
        { id: "sky", pt: "Azul céu", en: "big, pale sky-blue" },
        { id: "violet", pt: "Violeta", en: "big violet" },
        { id: "amethyst", pt: "Ametista", en: "big amethyst-purple" },
        { id: "emerald", pt: "Verde esmeralda", en: "big emerald green" },
        { id: "amber", pt: "Âmbar dourado", en: "big golden amber" },
        { id: "red", pt: "Vermelho", en: "big crimson red" },
        { id: "pink", pt: "Rosa", en: "big soft pink" },
        { id: "gray", pt: "Cinza", en: "big silver-gray" },
        { id: "hetero", pt: "Heterocromia azul e dourado", en: "big heterochromatic (one blue, one gold)" }
      ]
    },

    ears: {
      label: "Orelhas", name: "cat ears", group: "Personagem", default: "gray",
      options: [
        { id: "gray", pt: "Cinza com pelo branco", en: "fluffy gray cat ears with visible white inner fur" },
        { id: "match", pt: "Da cor do cabelo", en: "fluffy cat ears matching her hair color, with white inner fur" },
        { id: "black", pt: "Pretas com interior rosado", en: "black cat ears with soft pink inner ears" },
        { id: "white", pt: "Brancas bem fofas", en: "very fluffy white cat ears" },
        { id: "calico", pt: "Tricolor (calico)", en: "calico cat ears with orange, black and white patches" },
        { id: "tabby", pt: "Rajadas (tabby)", en: "striped tabby cat ears" },
        { id: "lynx", pt: "Pontudas com tufinho", en: "pointed cat ears with small lynx-like tufts" }
      ]
    },

    tail: {
      label: "Cauda", name: "tail", group: "Personagem", default: "thin",
      options: [
        { id: "thin", pt: "Fina e lisa", en: "a thin, smooth tail matching her ears that clearly comes out from her lower back and curves up in a gentle S shape" },
        { id: "fluffy", pt: "Grossa e peluda", en: "a fluffy tail matching her ears that clearly comes out from her lower back and curls to one side" },
        { id: "tabby", pt: "Rajada", en: "a striped tabby tail that clearly comes out from her lower back and curves up" },
        { id: "bell", pt: "Com lacinho e sininho", en: "a thin tail that clearly comes out from her lower back, with a small {{accentColor}} ribbon and a tiny bell near the tip" }
      ]
    },

    /* ----------------------------------------------------------------- Roupa */
    outfit: {
      label: "Roupa", name: "outfit", group: "Roupa", default: "cardigan",
      options: [
        // the two "build" options open the piece fields below instead of a ready outfit
        { id: "build", pt: "Montar: parte de cima + parte de baixo", en: "{{list:top,bottom,outerwear,legwear,shoes}}" },
        { id: "builddress", pt: "Montar: vestido", en: "{{list:dress,outerwear,legwear,shoes}}" },
        { id: "cardigan", pt: "Cardigã oversized + camiseta", en: "an oversized, cozy {{mainColor}} cardigan worn properly on both shoulders, with long sleeves covering half of her hands, over a loose {{accentColor}} t-shirt with a high neckline, polka-dot shorts and {{legwear}}" },
        { id: "hoodie", pt: "Moletom oversized", en: "an oversized {{mainColor}} hoodie with cat-ear details on the hood, sleeves covering half of her hands, {{accentColor}} shorts and {{legwear}}" },
        { id: "wintercoat", pt: "Casaco de inverno + cachecol", en: "a long {{mainColor}} wool coat with gold buttons over a dark dress with a subtle floral pattern, a long chunky {{accentColor}} knitted scarf, and {{legwear}}" },
        { id: "turtleneck", pt: "Gola alta + saia longa", en: "a soft {{mainColor}} turtleneck sweater tucked into a long pleated {{accentColor}} skirt, with {{legwear}}" },
        { id: "sweaterdress", pt: "Suéter-vestido oversized", en: "an oversized {{mainColor}} knit sweater dress worn properly on both shoulders, long sleeves covering her hands, a small {{accentColor}} ribbon at the collar, and {{legwear}}" },
        { id: "sundress", pt: "Vestido + cardigã leve", en: "a {{mainColor}} sundress with a high neckline under a light {{accentColor}} cardigan" },
        { id: "pajamas", pt: "Pijama fofinho", en: "cozy {{mainColor}} pajamas with a {{accentColor}} cat-paw pattern, long sleeves and long pants" },
        { id: "gothic", pt: "Gótico suave", en: "a modest gothic-lolita style {{mainColor}} dress with {{accentColor}} ribbons and lace trim, a high collar, long sleeves and {{legwear}}" },
        { id: "street", pt: "Streetwear", en: "an oversized {{mainColor}} bomber jacket over a {{accentColor}} hoodie, cargo pants and chunky sneakers" },
        { id: "cafe", pt: "Uniforme de café", en: "a café waitress uniform: a long-sleeved {{mainColor}} dress with a high collar, a frilly white apron with {{accentColor}} trim, and {{legwear}}" },
        { id: "yukata", pt: "Yukata", en: "a {{mainColor}} yukata with a {{accentColor}} floral pattern and a matching obi" },
        { id: "raincoat", pt: "Capa de chuva", en: "a glossy {{mainColor}} raincoat with the hood down, a {{accentColor}} sweater underneath and rubber boots" },
        { id: "office", pt: "Blusa + saia social", en: "a {{mainColor}} blouse with a soft bow at the collar, a high-waisted {{accentColor}} skirt and {{legwear}}" },
        { id: "sporty", pt: "Esportivo", en: "a zipped-up {{mainColor}} track jacket, {{accentColor}} track pants and sneakers" },
        { id: "witch", pt: "Bruxinha", en: "a cute witch outfit: a long {{mainColor}} dress, a short cape, and a wide-brim hat with a {{accentColor}} ribbon and openings for her cat ears" }
      ]
    },

    /* Pieces, used by the two "build" outfits. No commas inside: they go into a list.
       Patterns are adjectives that sit between the color and the garment. */
    dress: {
      label: "Vestido", name: "dress", group: "Roupa", default: "sweaterdress",
      options: [
        { id: "sweaterdress", pt: "Suéter-vestido oversized", en: "an oversized {{topColor}} {{topPattern}} knit sweater dress worn properly on both shoulders with long sleeves covering her hands" },
        { id: "smock", pt: "Vestido bata soltinho", en: "a loose {{topColor}} {{topPattern}} smock dress with long sleeves and a rounded collar" },
        { id: "pinafore", pt: "Jardineira (pinafore) sobre blusa", en: "a {{topColor}} {{topPattern}} pinafore dress over a white long-sleeved blouse" },
        { id: "shirtdress", pt: "Chemise com cinto", en: "a {{topColor}} {{topPattern}} button-up shirt dress with long sleeves and a soft belt" },
        { id: "summer", pt: "Vestido de verão com mangas bufantes", en: "a light {{topColor}} {{topPattern}} summer dress with a high neckline and short puffed sleeves" },
        { id: "lolita", pt: "Lolita suave", en: "a modest lolita-style {{topColor}} {{topPattern}} long-sleeved dress with lace trim and a high collar" },
        { id: "nightgown", pt: "Camisola longa de dormir", en: "a long loose {{topColor}} {{topPattern}} nightgown with long sleeves and a rounded collar" },
        { id: "knitlong", pt: "Vestido longo de tricô", en: "a long {{topColor}} {{topPattern}} knit dress with a high neckline and long sleeves" }
      ]
    },

    top: {
      label: "Parte de cima", name: "top", group: "Roupa", default: "oversizedsweater",
      options: [
        { id: "oversizedsweater", pt: "Suéter oversized (mangas nas mãos)", en: "an oversized {{topColor}} {{topPattern}} knit sweater worn properly on both shoulders with long sleeves covering half of her hands" },
        { id: "sweater", pt: "Suéter de tricô", en: "a soft {{topColor}} {{topPattern}} knit sweater with long sleeves" },
        { id: "turtleneck", pt: "Gola alta", en: "a soft {{topColor}} {{topPattern}} turtleneck sweater" },
        { id: "hoodie", pt: "Moletom com orelhinhas no capuz", en: "an oversized {{topColor}} {{topPattern}} hoodie with cat-ear details on the hood and sleeves covering half of her hands" },
        { id: "tshirt", pt: "Camiseta larga", en: "a loose {{topColor}} {{topPattern}} t-shirt with a high crew neckline" },
        { id: "blouse", pt: "Blusa com laço na gola", en: "a {{topColor}} {{topPattern}} blouse with long puffed sleeves and a soft bow at the high collar" },
        { id: "shirt", pt: "Camisa de botão fechada", en: "a {{topColor}} {{topPattern}} button-up shirt buttoned all the way up with long sleeves" },
        { id: "sailor", pt: "Blusa marinheiro", en: "a {{topColor}} {{topPattern}} sailor-style blouse with a wide collar and long sleeves" },
        { id: "vest", pt: "Colete de tricô sobre camisa", en: "a {{topColor}} {{topPattern}} knit vest over a white long-sleeved collared shirt" },
        { id: "pajamatop", pt: "Blusa de pijama", en: "a loose {{topColor}} {{topPattern}} pajama top with long sleeves and a rounded collar" }
      ]
    },

    topColor: {
      label: "Cor da peça principal", name: "main piece color", group: "Roupa", default: "cream",
      options: null // filled from COLORS below
    },

    topPattern: {
      label: "Estampa da peça principal", name: "main piece pattern", group: "Roupa", default: "none",
      options: null // filled from PATTERNS below
    },

    bottom: {
      label: "Parte de baixo", name: "bottom", group: "Roupa", default: "pleated",
      options: [
        { id: "pleated", pt: "Saia plissada até o joelho", en: "a knee-length {{bottomColor}} {{bottomPattern}} pleated skirt" },
        { id: "pleatedstripe", pt: "Saia plissada com listras brancas na barra", en: "a knee-length {{bottomColor}} {{bottomPattern}} pleated skirt with two thin white stripes near the hem" },
        { id: "aline", pt: "Saia evasê até o joelho", en: "a knee-length {{bottomColor}} {{bottomPattern}} A-line skirt" },
        { id: "suspender", pt: "Saia com suspensório", en: "a knee-length {{bottomColor}} {{bottomPattern}} skirt with thin suspenders" },
        { id: "longskirt", pt: "Saia longa", en: "a long flowing {{bottomColor}} {{bottomPattern}} skirt" },
        { id: "shorts", pt: "Short largo", en: "loose {{bottomColor}} {{bottomPattern}} shorts" },
        { id: "trousers", pt: "Calça reta", en: "straight-leg {{bottomColor}} {{bottomPattern}} trousers" },
        { id: "wideleg", pt: "Pantalona", en: "wide-leg {{bottomColor}} {{bottomPattern}} trousers" },
        { id: "cargo", pt: "Calça cargo", en: "loose {{bottomColor}} cargo pants" },
        { id: "sweatpants", pt: "Calça de moletom", en: "soft {{bottomColor}} sweatpants" },
        { id: "pajamapants", pt: "Calça de pijama", en: "loose {{bottomColor}} {{bottomPattern}} pajama pants" }
      ]
    },

    bottomColor: {
      label: "Cor da parte de baixo", name: "bottom color", group: "Roupa", default: "brown",
      options: null // filled from COLORS below
    },

    bottomPattern: {
      label: "Estampa da parte de baixo", name: "bottom pattern", group: "Roupa", default: "none",
      options: null // filled from PATTERNS below
    },

    outerwear: {
      label: "Por cima", name: "outerwear", group: "Roupa", default: "none",
      options: [
        { id: "none", pt: "Nada", en: "" },
        { id: "cardigan", pt: "Cardigã oversized", en: "an oversized {{outerColor}} cardigan worn properly on both shoulders" },
        { id: "coat", pt: "Casaco longo de lã", en: "a long {{outerColor}} wool coat with large buttons" },
        { id: "duffle", pt: "Casaco duffle com capuz", en: "a {{outerColor}} duffle coat with toggle buttons and a hood" },
        { id: "trench", pt: "Trench coat", en: "a {{outerColor}} trench coat with a soft belt" },
        { id: "bomber", pt: "Jaqueta bomber", en: "an oversized {{outerColor}} bomber jacket" },
        { id: "denim", pt: "Jaqueta jeans", en: "a {{outerColor}} denim jacket" },
        { id: "raincoat", pt: "Capa de chuva", en: "a glossy {{outerColor}} raincoat with the hood down" },
        { id: "capelet", pt: "Capinha curta com laço", en: "a short {{outerColor}} capelet tied with a ribbon" },
        { id: "poncho", pt: "Poncho de tricô", en: "a cozy {{outerColor}} knit poncho" },
        { id: "apron", pt: "Avental com babado", en: "a frilly {{outerColor}} apron" }
      ]
    },

    outerColor: {
      label: "Cor do que vai por cima", name: "outerwear color", group: "Roupa", default: "camel",
      options: null // filled from COLORS below
    },

    legwear: {
      label: "Meias", name: "legwear", group: "Roupa", default: "thighhigh",
      options: [
        { id: "none", pt: "Nenhuma à mostra", en: "" },
        { id: "thighhigh", pt: "Meia 7/8 escura", en: "dark gray thigh-high socks" },
        { id: "thighblack", pt: "Meia 7/8 preta", en: "black thigh-high socks" },
        { id: "thighstripe", pt: "Meia 7/8 preta com listras brancas no topo", en: "black thigh-high socks with two thin white stripes around the top" },
        { id: "tights", pt: "Meia-calça preta", en: "black tights" },
        { id: "kneewhite", pt: "Meia branca até o joelho", en: "white knee-high socks" },
        { id: "striped", pt: "Meia listrada", en: "striped knee-high socks in {{accentColor}} and white" },
        { id: "legwarmers", pt: "Polainas", en: "slouchy {{mainColor}} leg warmers" },
        { id: "ankle", pt: "Meia soquete", en: "short ankle socks" }
      ]
    },

    shoes: {
      label: "Calçado", name: "shoes", group: "Roupa", default: "none",
      options: [
        // "none" by default: the favorite framing (hips up) does not show the feet
        { id: "none", pt: "Não especificar", en: "" },
        { id: "loafers", pt: "Mocassim", en: "{{shoesColor}} loafers" },
        { id: "maryjanes", pt: "Sapato boneca (Mary Jane)", en: "{{shoesColor}} Mary Jane shoes" },
        { id: "sneakers", pt: "Tênis", en: "chunky {{shoesColor}} sneakers" },
        { id: "ankleboots", pt: "Bota curta", en: "{{shoesColor}} ankle boots" },
        { id: "rainboots", pt: "Galocha", en: "{{shoesColor}} rubber rain boots" },
        { id: "slippers", pt: "Pantufa de gatinho", en: "fluffy {{shoesColor}} cat-face slippers" },
        { id: "sandals", pt: "Sandália com tira", en: "{{shoesColor}} strap sandals" }
      ]
    },

    shoesColor: {
      label: "Cor do calçado", name: "shoes color", group: "Roupa", default: "brown",
      options: null // filled from COLORS below
    },

    // Used by the ready-made outfits and by small details (ribbons, bell, background tint).
    // Declared after the pieces so the piece fields sit right under "Roupa" in the UI.
    mainColor: {
      label: "Cor principal", name: "main clothing color", group: "Roupa", default: "beige",
      options: null // filled from COLORS below
    },

    accentColor: {
      label: "Cor de destaque", name: "accent color", group: "Roupa", default: "teal",
      options: null // filled from COLORS below
    },

    hairAccessory: {
      label: "Acessório de cabelo", name: "hair accessory", group: "Roupa", default: "ribbons",
      options: [
        { id: "none", pt: "Nenhum", en: "" },
        { id: "ribbons", pt: "Lacinhos", en: "Small {{accentColor}} ribbons tie her hair." },
        { id: "bigbows", pt: "Laços grandes", en: "Two large {{accentColor}} bows sit in her hair." },
        { id: "xclip", pt: "Grampo em X", en: "An X-shaped hairclip holds her bangs." },
        { id: "flower", pt: "Grampo de flor", en: "A small flower hairpin sits above one ear." },
        { id: "beret", pt: "Boina", en: "She wears a {{accentColor}} beret tilted between her cat ears." }
      ]
    },

    accessory: {
      label: "Acessório", name: "accessory", group: "Roupa", default: "none",
      options: [
        { id: "none", pt: "Nenhum", en: "" },
        { id: "bell", pt: "Gargantilha com sininho", en: "A black choker with a small golden bell sits at her neck." },
        { id: "glasses", pt: "Óculos redondos", en: "She wears thin round glasses." },
        { id: "headphones", pt: "Fone no pescoço", en: "Over-ear headphones rest around her neck." },
        { id: "pawgloves", pt: "Luvinhas de patinha", en: "She wears fluffy cat-paw mittens." },
        { id: "bag", pt: "Bolsinha de gato", en: "A small cat-shaped shoulder bag hangs at her side." }
      ]
    },

    /* ---------------------------------------------------- Clima e expressão */
    vibe: {
      label: "Clima", name: "mood", group: "Clima e expressão", default: "cozy",
      options: [
        { id: "cozy", pt: "Fofo e aconchegante", en: "The mood is cute and cozy, not sensual." },
        { id: "playful", pt: "Brincalhão", en: "The mood is playful and mischievous, like a cat about to knock something off a table, but never sensual." },
        { id: "cool", pt: "Calmo e frio", en: "The mood is calm, cool and a little aloof, not sensual." },
        { id: "sleepy", pt: "Sonolento", en: "The mood is sleepy, soft and quiet, not sensual." },
        { id: "elegant", pt: "Elegante", en: "The mood is elegant and graceful, not sensual." },
        { id: "gloomy", pt: "Gótico suave", en: "The mood is softly gloomy and gothic-cute, not sensual." },
        { id: "energetic", pt: "Animado", en: "The mood is bright and energetic, not sensual." }
      ]
    },

    focus: {
      label: "Destaque", name: "focus", group: "Clima e expressão", default: "face",
      options: [
        { id: "face", pt: "Rosto, cabelo e orelhas", en: "her face, hair and ears are the center of the image. Draw the hair with soft, airy strands and gentle highlights, the eyes large and expressive, and the ears fluffy and detailed. Draw the clothing as described but render it simply, with little extra detail, so it does not compete with the face." },
        { id: "balanced", pt: "Equilibrado", en: "the whole character reads clearly, with the face as the main point of interest and the outfit in a supporting role." },
        { id: "outfit", pt: "Roupa em destaque", en: "the outfit is shown clearly with nice fabric folds and details, while the face stays expressive and clean." }
      ]
    },

    expression: {
      label: "Expressão", name: "expression", group: "Clima e expressão", default: "hintsmug",
      options: [
        { id: "sweet", pt: "Sorriso doce", en: "a sweet, gentle closed-mouth smile, soft eyes fully open, light blush" },
        { id: "hintsmug", pt: "Doce com pitada de convencida", en: "a sweet smile with just a hint of playful smugness (one corner of the mouth slightly raised), eyes fully open and soft, not half-lidded, light blush" },
        { id: "smug", pt: "Convencida (forte)", en: "a confident, playful smirk with one eyebrow slightly raised, eyes fully open" },
        { id: "shy", pt: "Tímida", en: "a shy little smile, light blush, eyes slightly averted" },
        { id: "happy", pt: "Feliz, rindo", en: "a bright, happy laugh with eyes closed in joyful curves and an open smile" },
        { id: "surprised", pt: "Surpresa", en: "surprised, with a small open \"o\" mouth and wide eyes" },
        { id: "pout", pt: "Emburrada", en: "a small pout with one cheek slightly puffed, calm eyes" },
        { id: "bored", pt: "Entediada", en: "bored and unimpressed, mouth in a flat line, eyes calm" },
        { id: "sleepy", pt: "Com sono", en: "sleepy, with droopy half-closed eyes and a tiny yawn" },
        { id: "asleep", pt: "Dormindo", en: "peacefully asleep with eyes closed and a relaxed little smile" },
        { id: "aloof", pt: "Fria e distante", en: "calm and aloof, a neutral closed mouth, a steady gaze and no blush" },
        { id: "flustered", pt: "Envergonhada (muito)", en: "flustered and embarrassed, strong blush and a wavy, nervous mouth" },
        { id: "teary", pt: "Chorosa", en: "a teary-eyed pout with glossy eyes" },
        { id: "curious", pt: "Curiosa", en: "curious, lips slightly parted, wide and attentive eyes" },
        { id: "wink", pt: "Piscadinha", en: "a playful wink with a small, cheeky smile" },
        { id: "determined", pt: "Determinada", en: "determined, with a small confident smile and bright eyes" }
      ]
    },

    gaze: {
      label: "Olhar", name: "gaze", group: "Clima e expressão", default: "viewer",
      options: [
        { id: "viewer", pt: "Para quem vê", en: "looking at the viewer" },
        { id: "upward", pt: "De baixo para cima, para quem vê", en: "looking up at the viewer" },
        { id: "away", pt: "Para o lado", en: "looking away to the side" },
        { id: "down", pt: "Para baixo", en: "looking down shyly" },
        { id: "sky", pt: "Para o alto", en: "looking up toward the sky" },
        { id: "closed", pt: "Olhos fechados", en: "eyes closed" }
      ]
    },

    /* ------------------------------------------------------ Câmera e estilo */
    camera: {
      label: "Câmera", name: "camera and framing", group: "Câmera e estilo", default: "closeabove",
      options: [
        { id: "closeabove", pt: "Perto, de cima, inclinada no rosto", en: "close to her, framed from the hips up so her face is large in the frame; the camera is slightly above her eye level, looking gently down at her face, with a subtle tilt toward her face" },
        { id: "portrait", pt: "Close (cabeça e ombros)", en: "a head-and-shoulders close-up at eye level" },
        { id: "waist", pt: "Da cintura para cima", en: "framed from the waist up, camera at eye level" },
        { id: "knees", pt: "Dos joelhos para cima", en: "framed from the knees up, camera at eye level" },
        { id: "full", pt: "Corpo inteiro", en: "full body in frame with some space around her, camera at eye level" },
        { id: "fullabove", pt: "Corpo inteiro, de cima", en: "full body in frame, the camera slightly above her and looking down" },
        { id: "high", pt: "Bem de cima", en: "a high angle from above, looking down at her" },
        { id: "chestlow", pt: "Do peito para cima, de baixo", en: "framed from the chest up, the camera slightly below her eye level, looking up at her face" },
        { id: "dutch", pt: "Dinâmica (inclinada)", en: "a dynamic, slightly tilted (dutch) angle, framed from mid-thigh up" },
        { id: "selfie", pt: "Ponto de vista de selfie", en: "a selfie point of view from her extended arm, slightly above her, with a gentle wide-angle look" }
      ]
    },

    style: {
      label: "Traço", name: "art style", group: "Câmera e estilo", default: "ln2010",
      options: [
        { id: "ln2010", pt: "Light novel 2010s (sem cara de IA)", en: "hand-drawn anime illustration look, like a 2010s light novel illustration. Clean thin lineart with slight line-weight variation, soft two-tone cel shading, matte skin with no glossy or plastic shine, slightly desaturated colors, soft airy lighting. Avoid hyper-detailed textures and 3D-like rendering." },
        { id: "tvanime", pt: "Anime de TV (cel moderno)", en: "modern TV-anime cel shading: crisp lineart, flat colors with a single shadow tone, clean and bright. Matte skin, no glossy shine." },
        { id: "watercolor", pt: "Aquarela suave", en: "soft watercolor-like anime illustration with gentle color bleeding, a light paper texture and delicate lineart." },
        { id: "sketch", pt: "Rascunho colorido", en: "loose pencil-sketch anime style with visible construction lines and a light color wash." },
        { id: "chibi", pt: "Chibi", en: "chibi (super-deformed) style: oversized head, small rounded body, simple shapes and thick soft lineart." },
        { id: "keyvisual", pt: "Key visual polido (mais brilho)", en: "polished anime key-visual rendering with rich lighting, glossy highlights and detailed shading." }
      ]
    },

    palette: {
      label: "Paleta (temperatura de cor)", name: "color palette", group: "Câmera e estilo", default: "pastel",
      options: [
        { id: "pastel", pt: "Pastel neutra", en: "a soft pastel palette with a neutral color temperature" },
        { id: "warm", pt: "Quente (dourado e pêssego)", en: "a warm color temperature: golden and peach tones in the skin and hair, soft warm light coming from the left" },
        { id: "cool", pt: "Fria (sombras azuladas)", en: "a cool color temperature: bluish shadows, paler and cooler skin and hair tones, soft blue-white light coming from the left" },
        { id: "night", pt: "Noite", en: "a night palette: deep blue ambient light with a soft warm rim light along her outline" },
        { id: "sunset", pt: "Pôr do sol", en: "a sunset palette: an orange-pink glow from one side and long, soft shadows" },
        { id: "spring", pt: "Primavera", en: "a fresh spring palette of light greens and cherry-blossom pinks" },
        { id: "autumn", pt: "Outono", en: "an autumn palette of amber, maroon and mustard accents" },
        { id: "mono", pt: "Monocromática + destaque", en: "a muted monochrome palette with {{accentColor}} as the only accent color" }
      ]
    }
  }
};

/* Shared color list for clothing colors (pt label, en text). */
(function () {
  const COLORS = [
    ["beige", "Bege", "beige"],
    ["cream", "Creme", "cream-white"],
    ["white", "Branco", "white"],
    ["black", "Preto", "black"],
    ["charcoal", "Grafite", "charcoal gray"],
    ["lightgray", "Cinza claro", "light gray"],
    ["navy", "Azul marinho", "navy blue"],
    ["skyblue", "Azul claro", "light sky blue"],
    ["teal", "Verde-azulado", "teal"],
    ["mint", "Menta", "mint green"],
    ["sage", "Verde sálvia", "sage green"],
    ["forest", "Verde floresta", "forest green"],
    ["lavender", "Lavanda", "lavender"],
    ["plum", "Ameixa", "dark plum"],
    ["purple", "Roxo", "purple"],
    ["pink", "Rosa pastel", "pastel pink"],
    ["rose", "Rosa antigo", "dusty rose"],
    ["red", "Vermelho", "red"],
    ["burgundy", "Bordô", "burgundy"],
    ["mustard", "Mostarda", "mustard yellow"],
    ["orange", "Laranja", "soft orange"],
    ["camel", "Caramelo", "camel"],
    ["brown", "Marrom", "chocolate brown"]
  ].map(([id, pt, en]) => ({ id, pt, en }));

  /* Fabric patterns: adjectives placed between the color and the garment ("a cream striped sweater"). */
  const PATTERNS = [
    ["none", "Lisa", ""],
    ["stripes", "Listrada", "striped"],
    ["polka", "Bolinhas", "polka-dot"],
    ["plaid", "Xadrez", "plaid"],
    ["gingham", "Xadrez vichy", "gingham"],
    ["argyle", "Losangos (argyle)", "argyle"],
    ["floral", "Florzinhas", "small floral-print"],
    ["hearts", "Coraçõezinhos", "heart-print"],
    ["paws", "Patinhas de gato", "cat-paw-print"],
    ["stars", "Estrelinhas", "star-print"]
  ].map(([id, pt, en]) => ({ id, pt, en }));

  const V = window.CATALOG.vars;
  for (const name of ["mainColor", "accentColor", "topColor", "bottomColor", "outerColor", "shoesColor"]) V[name].options = COLORS;
  for (const name of ["topPattern", "bottomPattern"]) V[name].options = PATTERNS;
})();
