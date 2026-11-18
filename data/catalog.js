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
 * {{var.field}} is another field of the option chosen for var. Every option of that var
 * must have the field. Scenic backgrounds end in {{focus.bg}}, so the "Destaque" choice
 * decides whether the scenery is blurred or drawn sharp.
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
 *   default  option id used when nothing else sets it (a list of ids for a multi var)
 *   multi    true: several options at once, shown as chips (see hairAccessory)
 */
window.CATALOG = {
  groups: ["Pose", "Imagem", "Personagem", "Roupa", "Clima e expressão", "Câmera e estilo"],

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
    /* ------------------------------------------------------------------ Pose
       Variations of one prefab: its pose text is just "{{idlePose}}" (or mascotPose),
       so this field only shows up while that prefab is picked. */
    idlePose: {
      label: "Variação da pose", name: "pose", group: "Pose", default: "behind",
      options: [
        { id: "behind", pt: "Mãos para trás", en: "standing relaxed with her weight on one leg, both hands loosely clasped behind her back, head tilted slightly" },
        { id: "front", pt: "Mãos juntas na frente", en: "standing relaxed, both hands lightly holding each other in front of her, shoulders soft" },
        { id: "sleeves", pt: "Mãos escondidas nas mangas", en: "standing relaxed, both hands hidden inside her long sleeves and held close under her chin" },
        { id: "breeze", pt: "Cabelo ao vento", en: "standing relaxed in a gentle breeze, her hair and tail drifting to one side, one hand tucking a strand of hair behind her ear" },
        { id: "tiptoe", pt: "Na ponta dos pés, esperando", en: "standing on tiptoe with her hands clasped behind her back, as if waiting for someone" },
        { id: "bag", pt: "Segurando a alça da bolsa", en: "standing relaxed, one hand holding the strap of a small shoulder bag, the other hanging loosely at her side" },
        { id: "pockets", pt: "Mãos nos bolsos", en: "standing relaxed with both hands tucked into her pockets, head tilted to one side" },
        { id: "smallwave", pt: "Aceno tímido", en: "standing relaxed, giving a small, shy wave with one hand at shoulder height, the other hand at her side" },
        { id: "fidget", pt: "Mexendo na ponta do cabelo", en: "standing relaxed, twirling the end of a lock of hair around one finger, looking a little shy" }
      ]
    },

    mascotPose: {
      label: "Variação da pose", name: "pose", group: "Pose", default: "stand",
      options: [
        { id: "stand", pt: "Em pé na barra de tarefas", en: "standing on the bottom edge of the image as if it were the floor, relaxed, hands clasped behind her back" },
        { id: "wave", pt: "Acenando", en: "standing on the bottom edge of the image, waving at the viewer with one hand raised" },
        { id: "sitedge", pt: "Sentada na beirada, pernas balançando", en: "sitting on the bottom edge of the image as if it were a ledge, legs dangling over it, hands resting on the edge beside her" },
        { id: "knees", pt: "Sentada abraçando os joelhos", en: "sitting on the bottom edge of the image hugging her knees, tail wrapped around her feet" },
        { id: "lying", pt: "Deitada lendo", en: "lying on her stomach along the bottom edge of the image, chin propped on her hands over an open book, feet kicking lazily in the air behind her" },
        { id: "sleep", pt: "Dormindo enrolada", en: "curled up asleep on the bottom edge of the image like a cat, head resting on her folded arms, tail wrapped around her" },
        { id: "laptop", pt: "No notebook", en: "sitting cross-legged on the bottom edge of the image with a small laptop on her lap, typing and glancing at the viewer" },
        { id: "hang", pt: "Pendurada na borda de cima", en: "hanging by both hands from the top edge of the image, feet dangling, looking down at the viewer with a playful grin" }
      ]
    },

    /* ---------------------------------------------------------------- Imagem */
    format: {
      label: "Formato", name: "image format", group: "Imagem", default: "portrait",
      options: [
        { id: "portrait", pt: "Retrato 2:3 (só a personagem)", en: "Create a vertical anime illustration, 1024x1536." },
        { id: "mascot", pt: "Mascote de desktop (corpo inteiro)", en: "Create a vertical anime illustration, 1024x1536, of a desktop mascot: the whole character is in frame from head to toe with nothing cut off, so she can sit on top of a desktop wallpaper." },
        { id: "landscape", pt: "Paisagem 3:2 (wallpaper)", en: "Create a horizontal anime illustration, 1536x1024, composed as a desktop wallpaper: the character sits in the right third of the frame and the left two-thirds stay calm and mostly empty, with room for desktop icons." },
        { id: "square", pt: "Quadrado 1:1 (avatar)", en: "Create a square anime illustration, 1024x1024." },
        // exact monitor sizes: only the local studio (API) delivers them; ChatGPT keeps its own sizes
        { id: "wide", pt: "Wallpaper ultrawide 2560×1080", en: "Create a horizontal anime illustration, 2560x1080 (21:9 ultrawide), composed as a desktop wallpaper: the character sits in the right third of the frame and the left two-thirds stay calm and mostly empty, with room for desktop icons." },
        { id: "fhd", pt: "Wallpaper Full HD 1920×1080", en: "Create a horizontal anime illustration, 1920x1080 (16:9), composed as a desktop wallpaper: the character sits in the right third of the frame and the left two-thirds stay calm and mostly empty, with room for desktop icons." },
        { id: "vertical", pt: "Wallpaper vertical 1080×1920", en: "Create a vertical anime illustration, 1080x1920 (9:16), composed as a wallpaper for a vertical monitor: the character is the main subject in the lower two-thirds of the frame and the top stays calm and simple." }
      ]
    },

    background: {
      label: "Fundo", name: "background", group: "Imagem", default: "transparent",
      options: [
        { id: "transparent", pt: "Transparente (PNG)", en: "Fully transparent background (PNG), no floor shadow." },
        { id: "white", pt: "Branco liso (fácil de recortar)", en: "Plain solid white background, no shadow, no other elements." },
        { id: "tint", pt: "Cor suave lisa (tom da cor de destaque)", en: "Plain soft background in a very light tint of {{accentColor}}, no other elements." },
        { id: "bedroom", pt: "Quarto aconchegante", en: "Background: a cozy bedroom with fairy lights and plush pillows, {{focus.bg}}." },
        { id: "cafe", pt: "Café com janela chuvosa", en: "Background: a quiet café by a rainy window, warm lamps inside, {{focus.bg}}." },
        { id: "snow", pt: "Rua com neve à noite", en: "Background: a snowy street at night with warm shop lights and gently falling snow, {{focus.bg}}." },
        { id: "rooftop", pt: "Telhado ao pôr do sol", en: "Background: a rooftop at sunset with a distant city skyline, {{focus.bg}}." },
        { id: "library", pt: "Biblioteca", en: "Background: a warm library with tall wooden bookshelves, {{focus.bg}}." },
        { id: "sakura", pt: "Parque de cerejeiras", en: "Background: a park with cherry blossom trees and drifting petals, {{focus.bg}}." },
        { id: "window", pt: "Janela com chuva", en: "Background: a wide window with rain streaks on the glass and a grey-blue city behind it, {{focus.bg}}." },
        { id: "classroom", pt: "Sala de aula à tarde", en: "Background: a Japanese school classroom in the afternoon, warm sunlight through tall windows and rows of wooden desks, {{focus.bg}}." },
        { id: "hallway", pt: "Corredor da escola", en: "Background: a bright school hallway with tall windows and shoe lockers, {{focus.bg}}." },
        { id: "schoolroof", pt: "Terraço da escola", en: "Background: a school rooftop with a tall green wire fence and a clear blue sky with soft clouds, {{focus.bg}}." },
        { id: "schoolgate", pt: "Portão da escola com cerejeiras", en: "Background: a school gate lined with blooming cherry trees in spring, petals drifting, {{focus.bg}}." }
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
        // opens the hair part fields below instead of a ready-made hairstyle
        { id: "build", pt: "Montar por partes", en: "{{hairShape}}, with {{list:bangs,ahoge,sidelocks,hairDye}}" },
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

    /* Hair parts, used by the "build" hairstyle. The shape reads after "hair in";
       bangs, ahoge, side locks and dye go into a list, so they have no commas. */
    hairShape: {
      label: "Estilo do cabelo", name: "hair shape", group: "Personagem", default: "straight",
      options: [
        { id: "straight", pt: "Solto e liso", en: "a {{hairLength}}, straight style" },
        { id: "wavy", pt: "Solto e ondulado", en: "a {{hairLength}}, softly wavy style" },
        { id: "curly", pt: "Cacheado", en: "a {{hairLength}}, bouncy curly style" },
        { id: "layered", pt: "Repicado e volumoso", en: "a {{hairLength}}, airy layered cut with lots of volume" },
        { id: "messy", pt: "Bagunçado de quem acordou", en: "a {{hairLength}}, messy just-woke-up style" },
        { id: "twintails", pt: "Maria-chiquinha", en: "{{hairLength}}, fluffy twin tails" },
        { id: "lowtwintails", pt: "Maria-chiquinha baixa", en: "{{hairLength}} low twin tails resting on her shoulders" },
        { id: "ponytail", pt: "Rabo de cavalo alto", en: "a {{hairLength}} high ponytail" },
        { id: "sideponytail", pt: "Rabo de cavalo de lado", en: "a {{hairLength}} side ponytail" },
        { id: "braids", pt: "Duas tranças", en: "two {{hairLength}} loose braids" },
        { id: "sidebraid", pt: "Uma trança sobre o ombro", en: "a {{hairLength}} braid draped over one shoulder" },
        { id: "buns", pt: "Dois coquinhos", en: "two small buns, with the rest of her {{hairLength}} hair hanging loose" },
        { id: "halfup", pt: "Meio preso com coque", en: "a {{hairLength}} half-up style with a small bun" },
        { id: "hime", pt: "Hime (corte reto)", en: "a {{hairLength}} hime cut" },
        { id: "wolf", pt: "Wolf cut", en: "a {{hairLength}}, fluffy wolf cut" },
        { id: "bob", pt: "Chanel curto", en: "a short, soft bob" }
      ]
    },

    hairLength: {
      label: "Comprimento do cabelo", name: "hair length", group: "Personagem", default: "long",
      options: [
        { id: "short", pt: "Curto", en: "short" },
        { id: "shoulder", pt: "Na altura dos ombros", en: "shoulder-length" },
        { id: "medium", pt: "Médio", en: "medium-length" },
        { id: "long", pt: "Longo", en: "long" },
        { id: "verylong", pt: "Muito longo", en: "very long" },
        { id: "floor", pt: "Até o chão", en: "floor-length" }
      ]
    },

    bangs: {
      label: "Franja", name: "bangs", group: "Personagem", default: "wispy",
      options: [
        { id: "notset", pt: "Não especificar", en: "" },
        { id: "wispy", pt: "Leve e fiapada", en: "soft wispy bangs" },
        { id: "blunt", pt: "Reta (cortada)", en: "blunt straight-cut bangs" },
        { id: "side", pt: "De lado", en: "side-swept bangs" },
        { id: "curtain", pt: "Repartida no meio (cortina)", en: "curtain bangs parted in the middle" },
        { id: "long", pt: "Longa, quase nos olhos", en: "long bangs that brush her eyelashes" },
        { id: "nobangs", pt: "Sem franja (testa à mostra)", en: "no bangs and her forehead showing" }
      ]
    },

    ahoge: {
      label: "Ahoge (fio rebelde)", name: "ahoge", group: "Personagem", default: "none",
      options: [
        { id: "none", pt: "Nenhum", en: "" },
        { id: "single", pt: "Um fio", en: "a single ahoge sticking up from the top of her head" },
        { id: "double", pt: "Dois, tipo antena", en: "two ahoge sticking up like little antennae" },
        { id: "heart", pt: "Em forma de coração", en: "an ahoge curled into a small heart shape" }
      ]
    },

    sidelocks: {
      label: "Mechas laterais", name: "side locks", group: "Personagem", default: "none",
      options: [
        { id: "none", pt: "Nada de especial", en: "" },
        { id: "long", pt: "Longas emoldurando o rosto", en: "long side locks framing her face" },
        { id: "braided", pt: "Uma trancinha fina", en: "one thin braided side lock" },
        { id: "clipped", pt: "Presas com fita", en: "side locks tied with small ribbons" }
      ]
    },

    hairDye: {
      label: "Detalhe de cor no cabelo", name: "hair color detail", group: "Personagem", default: "none",
      options: [
        { id: "none", pt: "Nenhum", en: "" },
        { id: "tips", pt: "Pontas coloridas", en: "tips that fade into {{hairDyeColor}}" },
        { id: "inner", pt: "Mecha interna escondida", en: "a hidden inner layer dyed {{hairDyeColor}}" },
        { id: "streak", pt: "Uma mecha na frente", en: "a single {{hairDyeColor}} streak near her face" }
      ]
    },

    hairDyeColor: {
      label: "Cor do detalhe", name: "hair detail color", group: "Personagem", default: "pink",
      options: null // filled from COLORS below
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
        { id: "witch", pt: "Bruxinha", en: "a cute witch outfit: a long {{mainColor}} dress, a short cape, and a wide-brim hat with a {{accentColor}} ribbon and openings for her cat ears" },
        // school uniforms: skirts stay knee-length (house rule)
        { id: "sailor", pt: "Escola: uniforme marinheiro", en: "a {{mainColor}} sailor school uniform with long sleeves, a wide collar with two white stripes, a {{accentColor}} neckerchief and a knee-length pleated skirt, with {{legwear}}" },
        { id: "blazer", pt: "Escola: blazer + saia xadrez", en: "a school uniform: a {{mainColor}} blazer with an emblem on the chest over a white collared shirt with a {{accentColor}} ribbon tie, a knee-length plaid pleated skirt and {{legwear}}" },
        { id: "schoolvest", pt: "Escola: colete + camisa", en: "a school uniform: a {{mainColor}} knit vest over a white short-sleeved collared shirt with a {{accentColor}} ribbon, a knee-length plaid pleated skirt and {{legwear}}" },
        { id: "schoolcardigan", pt: "Escola: cardigã sobre a camisa", en: "a school uniform: an oversized {{mainColor}} cardigan worn properly on both shoulders, with sleeves covering half of her hands, over a white collared shirt with a {{accentColor}} ribbon, a knee-length plaid pleated skirt and {{legwear}}" },
        { id: "gym", pt: "Escola: educação física", en: "a school gym uniform: a zipped-up {{mainColor}} track jacket with {{accentColor}} stripes down the sleeves and matching long track pants" }
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
        { id: "knitlong", pt: "Vestido longo de tricô", en: "a long {{topColor}} {{topPattern}} knit dress with a high neckline and long sleeves" },
        { id: "sailordress", pt: "Vestido marinheiro", en: "a {{topColor}} {{topPattern}} sailor dress with a wide collar and long sleeves" },
        { id: "hoodiedress", pt: "Moletom-vestido com orelhinhas", en: "an oversized {{topColor}} {{topPattern}} hoodie dress with cat-ear details on the hood and sleeves covering her hands" },
        { id: "apron", pt: "Vestido avental (estilo café)", en: "a long-sleeved {{topColor}} {{topPattern}} dress with a high collar under a frilly white apron" },
        { id: "tiered", pt: "Vestido longo em camadas", en: "a long tiered {{topColor}} {{topPattern}} dress with long puffed sleeves and a rounded collar" }
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
        { id: "pajamatop", pt: "Blusa de pijama", en: "a loose {{topColor}} {{topPattern}} pajama top with long sleeves and a rounded collar" },
        { id: "schoolshirt", pt: "Camisa de uniforme com laço", en: "a {{topColor}} {{topPattern}} collared school shirt with a small {{accentColor}} ribbon at the neck" },
        { id: "fleece", pt: "Blusa de fleece fofinha", en: "a fluffy {{topColor}} {{topPattern}} fleece pullover with a high zip collar" },
        { id: "polo", pt: "Camisa polo de manga longa", en: "a {{topColor}} {{topPattern}} long-sleeved polo shirt" },
        { id: "henley", pt: "Blusa de botãozinho (henley)", en: "a soft {{topColor}} {{topPattern}} long-sleeved henley top buttoned up to the collar" },
        { id: "ruffle", pt: "Blusa com gola de babado", en: "a {{topColor}} {{topPattern}} blouse with a high ruffled collar and long sleeves" }
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
        { id: "pajamapants", pt: "Calça de pijama", en: "loose {{bottomColor}} {{bottomPattern}} pajama pants" },
        { id: "denimskirt", pt: "Saia jeans até o joelho", en: "a knee-length {{bottomColor}} denim skirt" },
        { id: "tieredskirt", pt: "Saia longa em camadas", en: "a long tiered {{bottomColor}} {{bottomPattern}} skirt" },
        { id: "culottes", pt: "Pantacourt", en: "wide {{bottomColor}} {{bottomPattern}} culottes that reach below the knee" },
        { id: "jeans", pt: "Calça jeans larga", en: "loose {{bottomColor}} jeans" },
        { id: "overalls", pt: "Jardineira (calça)", en: "loose {{bottomColor}} denim overalls" }
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
        { id: "apron", pt: "Avental com babado", en: "a frilly {{outerColor}} apron" },
        { id: "blazer", pt: "Blazer de uniforme", en: "a {{outerColor}} school blazer with an emblem on the chest" },
        { id: "varsity", pt: "Jaqueta college", en: "a {{outerColor}} varsity jacket with cream sleeves" },
        { id: "fleecejacket", pt: "Jaqueta de fleece", en: "a fluffy {{outerColor}} fleece jacket" },
        { id: "haori", pt: "Haori", en: "a light {{outerColor}} haori jacket with a subtle pattern" },
        { id: "puffer", pt: "Jaqueta puffer", en: "a puffy {{outerColor}} winter jacket with a fur-trimmed hood down" }
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
        { id: "ankle", pt: "Meia soquete", en: "short ankle socks" },
        { id: "kneeblack", pt: "Meia preta até o joelho", en: "black knee-high socks" },
        { id: "thighwhite", pt: "Meia 7/8 branca", en: "white thigh-high socks" },
        { id: "frilly", pt: "Soquete com babadinho", en: "white ankle socks with a little lace frill" },
        { id: "pawsocks", pt: "Meia com patinhas", en: "{{accentColor}} knee-high socks with a small cat-paw print" }
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
        { id: "sandals", pt: "Sandália com tira", en: "{{shoesColor}} strap sandals" },
        { id: "uwabaki", pt: "Sapatilha de escola (uwabaki)", en: "white indoor school slippers with {{shoesColor}} toes" },
        { id: "canvas", pt: "Tênis de lona", en: "{{shoesColor}} canvas sneakers" },
        { id: "platform", pt: "Sapato boneca de plataforma", en: "chunky {{shoesColor}} platform Mary Jane shoes" }
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

    /* Multi-choice: the value is a list of ids, shown as chips, and each chosen option adds
       its own sentence. Options with the same "slot" exclude each other (one hat, one thing
       around the neck...): picking one takes the other off. Nothing chosen says nothing. */
    hairAccessory: {
      label: "Acessórios de cabelo", name: "hair accessories", group: "Roupa", default: ["ribbons"], multi: true,
      options: [
        { id: "ribbons", pt: "Lacinhos", slot: "bows", en: "Small {{accentColor}} ribbons tie her hair." },
        { id: "bigbows", pt: "Laços grandes", slot: "bows", en: "Two large {{accentColor}} bows sit in her hair." },
        { id: "xclip", pt: "Grampo em X", slot: "bangs", en: "An X-shaped hairclip holds her bangs." },
        { id: "starpins", pt: "Presilhas de estrelinha", slot: "bangs", en: "Two small star-shaped hairpins hold her bangs to one side." },
        { id: "flower", pt: "Grampo de flor", en: "A small flower hairpin sits above one ear." },
        { id: "beret", pt: "Boina", slot: "head", en: "She wears a {{accentColor}} beret tilted between her cat ears." },
        { id: "beanie", pt: "Gorro de tricô com furos para as orelhas", slot: "head", en: "She wears a soft {{accentColor}} knit beanie with openings for her cat ears." },
        { id: "headband", pt: "Tiara de laço", slot: "head", en: "A {{accentColor}} headband with a small bow sits in front of her cat ears." }
      ]
    },

    accessory: {
      label: "Acessórios", name: "accessories", group: "Roupa", default: [], multi: true,
      options: [
        { id: "glasses", pt: "Óculos redondos", en: "She wears thin round glasses." },
        { id: "bell", pt: "Gargantilha com sininho", slot: "neck", en: "A black choker with a small golden bell sits at her neck." },
        { id: "scarf", pt: "Cachecol de tricô", slot: "neck", en: "A soft knitted {{accentColor}} scarf is wrapped loosely around her neck." },
        { id: "headphones", pt: "Fone no pescoço", slot: "neck", en: "Over-ear headphones rest around her neck." },
        { id: "pawgloves", pt: "Luvinhas de patinha", slot: "hands", en: "She wears fluffy cat-paw mittens." },
        { id: "mittens", pt: "Luvas de tricô", slot: "hands", en: "She wears fluffy knitted {{accentColor}} mittens." },
        { id: "bag", pt: "Bolsinha de gato", slot: "bag", en: "A small cat-shaped shoulder bag hangs at her side." },
        { id: "backpack", pt: "Mochila escolar", slot: "bag", en: "A {{accentColor}} school backpack hangs from one shoulder." },
        { id: "keychain", pt: "Bolsa com chaveiro de gatinho", slot: "bag", en: "A small school bag with a dangling cat charm hangs from her hand." }
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
        // "bg" is how a scenic background is drawn: backgrounds with scenery end in {{focus.bg}}
        { id: "face", pt: "Rosto, cabelo e orelhas", en: "her face, hair and ears are the center of the image. Draw the hair with soft, airy strands and gentle highlights, the eyes large and expressive, and the ears fluffy and detailed. Draw the clothing as described but render it simply, with little extra detail, so it does not compete with the face.",
          bg: "softly blurred and low-detail so the character stands out" },
        { id: "balanced", pt: "Equilibrado", en: "the whole character reads clearly, with the face as the main point of interest and the outfit in a supporting role.",
          bg: "softly blurred and low-detail so the character stands out" },
        { id: "outfit", pt: "Roupa em destaque", en: "the outfit is shown clearly with nice fabric folds and details, while the face stays expressive and clean.",
          bg: "softly blurred and low-detail so the character stands out" },
        { id: "scenery", pt: "Cenário em destaque (fundo nítido)", en: "the scenery shares the spotlight with her: the place is drawn with care and rich, believable detail, like a background painting from an anime film, and she still reads clearly as the main character, in the same art style as the background.",
          bg: "drawn sharp, detailed and fully in focus, with no blur and no depth-of-field effect" }
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
  for (const name of ["mainColor", "accentColor", "topColor", "bottomColor", "outerColor", "shoesColor", "hairDyeColor"]) V[name].options = COLORS;
  for (const name of ["topPattern", "bottomPattern"]) V[name].options = PATTERNS;
})();
