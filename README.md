# Catgirl Prompt Builder

Gerador de prompts para ilustrações de catgirls no GPT. Você escolhe uma **pose
(prefab)**, ajusta a personagem nos campos (cabelo, olhos, roupa, cores,
expressão, câmera, estilo, fundo) e copia o prompt ou abre o ChatGPT já com ele
preenchido.

Os prompts saem em inglês, porque é o que o gerador de imagem entende melhor. As
opções da tela estão em português.

## Usar

Abra a página publicada (o link fica com quem publicou) ou publique de novo a
partir desta pasta. A página é `page.html` com `app.js`, `engine.js` e `data/`.

- **Poses**: a coluna da esquerda. Trocar de pose muda só pose, câmera,
  expressão e clima; a personagem continua a mesma.
- **Cadeado**: trava um campo, que deixa de mudar ao trocar de pose ou ao sortear.
- **Personalizado**: em qualquer campo dá para escrever o texto à mão (em inglês).
- **Ajuste rápido**: depois de copiar ou abrir um prompt, mude o que quiser e a
  página monta o pedido "Edit the last image… Change only…". Isso mantém o resto
  da imagem igual em vez de gerar outra do zero.
- **Favoritos**: salvam a combinação neste navegador.

## Como o prompt é montado

`data/catalog.js` tem as variáveis e os blocos. O prompt começa em
`CATALOG.blocks.prompt` e cada `{{nome}}` vira, nesta ordem:

1. `{{pose}}`: o texto de pose do prefab escolhido;
2. um bloco de `CATALOG.blocks` (expandido de novo);
3. uma variável de `CATALOG.vars`: o texto `en` da opção escolhida, que também
   pode ter placeholders (as roupas usam `{{mainColor}}`, `{{accentColor}}` e
   `{{legwear}}`).

A tela mostra só as variáveis que o prompt realmente usa. Uma roupa sem meias,
por exemplo, esconde o campo "Meias".

### Regras fixas da casa

Estão em `CATALOG.blocks` e entram em todo prompt. Vêm dos testes com o GPT:

- **personagem adulta** ("an adult cat-girl"): o GPT recusa mais quando lê a
  personagem como jovem;
- **roupa sem decote e sem foco no corpo**: sem isso o GPT puxa para o sensual,
  mesmo quando ninguém pediu;
- **"Create a completely new, original image…"**: sem isso ele reaproveita
  imagens anteriores da conversa;
- **cauda saindo da lombar**: sem isso a cauda aparece flutuando;
- **temperatura de cor explícita** no campo Paleta: "warm/cold" sozinho ele
  entende como personalidade, não como cor;
- **estilo "light novel 2010s"** como padrão: pele fosca e sombra em dois tons
  tiram a cara de IA.

## Criar um prefab

Adicione um objeto em `window.PREFABS` (`data/prefabs.js`):

```js
{
  id: "guitarra",                 // fixo: os favoritos guardam este id
  name: "Tocando violão",          // nome na tela
  category: "Sentada e deitada",   // uma das CATEGORIES
  desc: "Sentada no banco, dedilhando um violão.",
  pose: "sitting on a stool, playing an acoustic guitar, head tilted toward the strings, tail swaying to the rhythm",
  defaults: { camera: "knees", expression: "sweet", gaze: "down", vibe: "cozy" }
}
```

- `pose` em inglês, sem ponto final. Pode usar qualquer variável, por exemplo
  `{{accentColor}}`.
- `defaults` aceita qualquer variável do catálogo pelo `id` da opção. Deixe de fora
  os campos da personagem (cabelo, olhos…) para ela continuar a mesma entre poses.

Para uma nova opção em um campo existente, adicione `{ id, pt, en }` na lista
`options` da variável. Para uma variável nova, crie-a em `CATALOG.vars` e use
`{{nome}}` em algum bloco ou pose.

## Conferir antes de publicar

```bash
pip install mini-racer
python tools/check_prefabs.py
```

Roda todos os prefabs e todas as opções de todas as variáveis e aponta
placeholder não resolvido, default inválido, id duplicado e espaço ou pontuação
quebrados. Com `--show`, imprime dois prompts de exemplo.

## Depois de gerar

O GPT entrega 1024×1536. Para wallpaper 4K vale passar a imagem por um upscaler
de anime 2x (Real-ESRGAN `realesr-animevideov3`) e limpar a transparência. O
fundo transparente do GPT costuma vir com resíduo de 1–2/255 e a personagem com
opacidade máxima de 254.

`tools/postprocess.py` faz as duas coisas:

```bash
python tools/postprocess.py imagem.png                  # grava 2x/imagem.png ao lado
python tools/postprocess.py C:\Users\voce\Pictures\Catgirls   # todas as imagens da pasta
python tools/postprocess.py imagem.png --no-upscale     # só limpa: limpo/imagem.png
python tools/postprocess.py imagem.png --out wallpapers --force
```

- **Limpeza do alpha:** `alpha' = clamp((a − 4) / (249 − 4))`. Só roda quando a
  imagem tem cara de saída crua do GPT (alpha máximo abaixo de 255); force com
  `--clean always` ou desligue com `--clean never`.
- **Upscale 2x:** cor e alpha passam separados pelo modelo. Antes, a cor das
  bordas é espalhada por baixo dos pixels transparentes, para não sair halo.
  Depois, o alpha ampliado tem as pontas acertadas (≤ 5 vira 0, ≥ 252 vira 255).
- **Não use o `realesrgan-x4plus-anime`:** ele apaga textura fina (bolinhas,
  estampa).
- Saída que já existe é pulada; `--force` refaz.

Precisa só de Pillow (`pip install pillow`) e do executável do Real-ESRGAN em
`tools/bin/realesrgan-ncnn-vulkan/`, que fica fora do git: baixe
`realesrgan-ncnn-vulkan-20220424-windows.zip` da
[release v0.2.5.0](https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.5.0)
e extraia nessa pasta (ou passe `--esrgan caminho\do\exe`). Usa a GPU via Vulkan.
