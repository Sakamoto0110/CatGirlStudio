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

`{{list:a,b,c}}` expande cada variável, descarta as vazias (opção "nenhum") e
junta o resto como "x, y and z". É assim que funcionam as duas roupas "Montar…":
elas abrem os campos de peça (parte de cima, parte de baixo ou vestido, o que vai
por cima, meias e calçado, cada um com cor e, nas peças principais, estampa). O
penteado "Montar por partes" faz o mesmo com estilo, comprimento, franja, ahoge,
mechas laterais e detalhe de cor. Nessas opções montadas, o Ajuste rápido pede só
a parte que mudou. O
texto de uma peça não pode ter vírgula, senão a lista se embaralha. Uma opção com
texto vazio também apaga a palavra que ficaria pendurada antes dela ("shorts and
{{legwear}}" vira "shorts").

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

**Uma escolha mudando texto em outro lugar:** `{{var.campo}}` vira outro campo
da opção escolhida em `var`. Os fundos com cenário terminam em `{{focus.bg}}`:
com Destaque em rosto, equilibrado ou roupa, o cenário sai desfocado; com
"Cenário em destaque", sai nítido e detalhado. Toda opção da variável precisa
ter o campo, e o `check_prefabs.py` acusa se faltar.

**Vários ao mesmo tempo:** uma variável com `multi: true` (os acessórios) vira
botões que ligam e desligam, e o valor é uma lista de ids. Cada opção escolhida
acrescenta sua própria frase. Opções com o mesmo `slot` se excluem: escolher a
boina tira o gorro, escolher o cachecol tira a gargantilha. Favoritos e estados
salvos de antes, com um acessório só, são convertidos sozinhos.

**Pose com variações:** um prefab cujo `pose` é só uma variável (`"{{idlePose}}"`,
`"{{mascotPose}}"`) abre o campo "Variação da pose" quando é escolhido, e cada
opção da variável é uma pose completa. É assim que funcionam o "Idle" e o
"Mascote de desktop". A variável fica no grupo `"Pose"`.

## Conferir antes de publicar

```bash
pip install mini-racer
python tools/check_prefabs.py
```

Roda todos os prefabs e todas as opções de todas as variáveis e aponta
placeholder não resolvido, default inválido, id duplicado e espaço ou pontuação
quebrados. Com `--show`, imprime dois prompts de exemplo.

## Gerar pela API (estúdio local)

```bash
python tools/studio.py
```

Abre a mesma página em http://127.0.0.1:8770 com um painel "Gerar pela API":

- **Modelo**: Sunburst (mais cuidadoso, melhor para ajustes) ou Flare (mais
  rápido), ambos GPT-Image-2.6 e com o mesmo preço por token.
- **Qualidade**: medium, high ou xhigh. O botão mostra o custo estimado pela
  calculadora da OpenAI (1024×1536: ~US$ 0,03 / 0,04 / 0,07); o custo real vem
  do uso que a API devolve e fica salvo com a imagem.
- **Quantidade**: de 1 a 4 imagens com o mesmo prompt.
- **Aplicar nesta imagem** (no Ajuste rápido): manda a imagem escolhida junto
  com o pedido "Edit this image… Change only…". Clicar numa miniatura volta os
  campos para como aquela imagem foi feita.
- **Fazer 2x**: roda o `tools/postprocess.py` na imagem.

O tamanho segue o campo Formato, e o fundo transparente vira `background:
transparent` na API. Os formatos de wallpaper (2560×1080, 1920×1080 e
1080×1920) saem no tamanho exato da tela: a API só aceita lados múltiplos de 16,
então o estúdio pede 8 px a mais e corta pelo centro ao salvar. No ChatGPT esses
formatos não têm efeito no tamanho; lá a imagem sai nos tamanhos dele. Cada imagem vai para `Pictures\Catgirls\api` (mude com
`--out`) com um `.json` ao lado: prompt, seleção dos campos, modelo, qualidade,
custo, uso de tokens e, nos ajustes, a imagem de origem.

A chave vem da variável de ambiente `OpenAI_CatGirl_API`. No Windows o estúdio
também lê a variável salva no usuário, então funciona mesmo se ela foi criada
depois de abrir o terminal. A chave não aparece na página nem no console. O
servidor só escuta em 127.0.0.1 e recusa pedidos que não venham da própria página,
para outro site aberto no navegador não conseguir gastar a chave.

Com `--fake` o estúdio desenha imagens de teste no lugar da API e não cobra nada.
A página publicada não tem esse painel: ela não carrega o `studio.js`.

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
