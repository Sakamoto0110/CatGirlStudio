# Catgirl Studio

App para Windows (WPF, .NET 10) que monta prompts de ilustrações de catgirls e gera
e ajusta as imagens pela API da OpenAI. Você escolhe uma **pose (prefab)**, monta a
personagem (cabelo, olhos, estilo, roupa, acessórios, clima, câmera, traço, fundo)
e gera, ou copia o prompt para o ChatGPT.

Os prompts saem em inglês, porque é o que o gerador de imagem entende melhor. As
opções da tela estão em português.

```bash
dotnet publish src/CatgirlStudio.App -c Release
```

Gera `publish\CatgirlStudio.exe`, um arquivo só (~800 KB), que usa o .NET 10
Desktop Runtime já instalado. O catálogo e o ícone vão dentro do `.exe`.

## Usar

Quatro colunas:

1. **Poses**: busca, categorias e os cartões. Trocar de pose muda só pose,
   câmera, expressão e clima; a personagem continua a mesma.
2. **Personagem e cena**: só os campos que o prompt usa. Cada campo tem o botão
   de sortear e o cadeado (não muda ao trocar de pose nem ao sortear).
   "Personalizado…" deixa escrever o texto à mão, em inglês. Acessórios são chips
   que ligam e desligam. A **roupa** tem o próprio editor (abaixo).
3. **Prompt**: copiar ou abrir no ChatGPT, o **Ajuste rápido** (o que mudou desde
   a última imagem) e os **favoritos**.
4. **Gerar pela API**: modelo (Sunburst, mais cuidadoso e melhor para ajustes;
   Flare, mais rápido), qualidade (medium, high, xhigh) e quantidade (1 a 4). O
   botão mostra o custo estimado; o custo real vem do uso que a API devolve.
   **Aplicar nesta imagem** (no Ajuste rápido) manda a imagem junto com o pedido
   de ajuste. Clicar numa miniatura volta os campos para como ela foi feita.
   **Fazer 2x** limpa a transparência e amplia (veja abaixo).

Cada imagem vai para `Pictures\Catgirls\api` com um `.json` ao lado: prompt,
campos, modelo, qualidade, custo e, nos ajustes, a imagem de origem. Os formatos
de monitor (2560×1080, 1920×1080, 1080×1920) saem no tamanho exato da tela (a API
só aceita múltiplos de 16; o app pede 8 px a mais e corta pelo centro).

A chave vem da variável de ambiente `OpenAI_CatGirl_API` e só vai no cabeçalho do
pedido à OpenAI. Estado, favoritos e preferências ficam em `%AppData%\CatgirlStudio`.

| Opção | O que faz |
|---|---|
| `--fake` | modo de teste: desenha imagens no lugar da API, não cobra nada |
| `--out PASTA` / `--data PASTA` | outra pasta para as imagens / para estado e favoritos |
| `--accent #RRGGBB` | testa outra cor de destaque sem recompilar |
| `--screenshot ARQ.png` | salva uma imagem da janela e fecha |
| `--fake --selftest ARQ` | percorre o fluxo principal (pose, roupa, acessórios, gerar, ajuste, histórico, wallpaper, 2x) e escreve o resultado em ARQ |

### Cor de destaque

Uma linha em `src/CatgirlStudio.App/Theme/Theme.xaml`:

```xml
<Color x:Key="AccentColor">#F5B1C8</Color>
```

Os tons derivados (hover, fundo suave, texto sobre o destaque) saem dela em
`ThemeColors.cs`. O resto do visual (cartões, chips, listas, barras de rolagem)
também está no `Theme.xaml`.

## Roupa: OutfitEngine

`data/outfits.json` diz o que cada peça deixa mudar; nada além disso aparece na tela
ou entra no prompt (`src/CatgirlStudio.Core/OutfitEngine.cs`).

- **Tipos** (`modes`): *Pronta* (uma roupa inteira), *Peças* (parte de cima, por
  cima, parte de baixo), *Vestido*. Cada tipo usa algumas partes do corpo (`slots`):
  `full`, `dress`, `top`, `outer`, `bottom`, `legs` (meias), `feet` (calçado).
- **Peças**: cada uma tem `pt` (tela), `en` (prompt) e `props`, as propriedades que
  dá para mudar: `color`, `pattern`, `sleeves`, `hood`, `neckwear`, `skirtLength`,
  `main`/`accent` (nas roupas prontas). No texto, `{prop}` vira a opção escolhida;
  `{{accentColor}}` e outras variáveis do catálogo também valem.
- **Controle**: as `props` de um slot valem para todas as peças dele; uma peça pode
  acrescentar, mudar o padrão (`default`), limitar as opções (`allow`), fixar um
  valor (`fixed`, aparece mas não muda) ou tirar uma (`"pattern": null`, como na
  calça jeans). Um padrão `"@accentColor"` acompanha a cor de destaque.
- **Regras**: `legs: true` mostra o campo Meias (saia, short, vestido curto); sem
  isso, nada de meias. `covers: ["feet"]` esconde o calçado (roupa pronta que já
  diz o sapato). `theme: "idol"` só oferece a peça com aquele Estilo.
- **Casa**: gola alta, mangas, saia no joelho ou mais longa; não existe opção
  mais curta.

No prompt, as partes viram "x, y and z" na ordem do tipo. O Ajuste rápido compara
parte por parte ("- top: …", "- legwear: …"). Favoritos e imagens antigas, de antes
do OutfitEngine, são convertidos ao carregar.

## Como o prompt é montado

`data/catalog.json` tem os blocos e as variáveis; `data/prefabs.json`, as poses. O
prompt começa em `blocks.prompt` e cada `{{nome}}` vira, nesta ordem:

1. `{{pose}}`: o texto de pose do prefab escolhido;
2. um bloco de `blocks` (expandido de novo);
3. `{{outfit}}`: a roupa, do OutfitEngine;
4. uma variável de `vars`: o texto `en` da opção escolhida (que também pode ter
   placeholders).

As cores (com `hex`, para as amostras na tela) e as estampas são listas
compartilhadas em `lists`.

- `{{list:a,b,c}}` junta os itens não vazios como "x, y and z" (o penteado
  "Montar por partes" usa isso). Texto de item de lista não pode ter vírgula.
- `{{var.campo}}` lê outro campo da opção escolhida: os fundos com cenário
  terminam em `{{focus.bg}}`, então o Destaque decide se o cenário sai desfocado
  ou nítido.
- `"multi": true` (acessórios): vários ao mesmo tempo; opções com o mesmo `slot`
  se excluem (um chapéu, uma coisa no pescoço).
- Uma pose que é só uma variável (`"{{idlePose}}"`) abre "Variação da pose".
- O **Estilo** (`theme`) acrescenta uma frase, abre o próprio detalhe e libera as
  roupas `theme` do `outfits.json`.
- Uma opção com texto vazio apaga a palavra que ficaria pendurada antes dela
  ("with {neckwear}" + "Nada" some inteiro).

### Regras fixas da casa

Vêm dos testes com o GPT: personagem **adulta** ("an adult cat-girl"); **nada
sensual** (roupa sem decote e sem foco no corpo); **"Create a completely new,
original image…"**; **cauda saindo da lombar**; **temperatura de cor explícita** na
Paleta; traço **"light novel 2010s"** como padrão (pele fosca, sombra em dois tons).

## Criar um prefab

Acrescente um objeto em `prefabs` (`data/prefabs.json`):

```json
{"id": "guitarra", "name": "Tocando violão", "category": "Sentada e deitada", "desc": "Sentada no banco, dedilhando um violão.", "pose": "sitting on a stool, playing an acoustic guitar, head tilted toward the strings, tail swaying to the rhythm", "defaults": {"camera": "knees", "expression": "sweet", "gaze": "down", "vibe": "cozy", "outfit": "sweaterdress"}}
```

`id` é fixo (favoritos e imagens guardam). `defaults` aceita qualquer variável pelo
`id` da opção; `"outfit": "<id>"` escolhe uma roupa pronta. Deixe de fora os campos
da personagem para ela continuar a mesma entre poses. Depois de mudar o catálogo,
rode a checagem e gere o `.exe` de novo.

## Conferir

```bash
dotnet run --project tools/CatalogCheck
```

Checa o catálogo e as roupas: placeholder não resolvido, padrão inválido, id ou
variável duplicados, campo que nunca aparece, vírgula em lista, espaço ou pontuação
quebrados, toda peça em todo tipo com toda opção, `{prop}` sem declaração ou
declarada sem uso. Com `--show`, imprime um prompt de exemplo. Com
`--compare-web`, mostra como os prompts do app diferem dos da página web
(congelada) para as mesmas seleções antigas.

## Ampliar (2x, 3x, 4x)

**Ampliar** limpa a transparência suja do GPT (`alpha' = clamp((a − 4) / 245)`),
espalha a cor por baixo dos pixels transparentes para não sair halo, amplia cor e
alpha separados com o Real-ESRGAN (`realesr-animevideov3`; o `x4plus-anime` apaga
textura fina) e acerta as pontas do alpha.

- **Escala**: 2x, 3x ou 4x (1024×1536 vira até 4096×6144). Na RX 580, 2x leva uns
  4 s e 4x uns 6 s.
- **TTA**: roda o modelo em 8 cópias viradas da imagem e tira a média; bordas um
  pouco mais limpas, umas 5 a 8 vezes mais lento (2x com TTA: uns 20 s).
- Cada combinação vai para a sua pasta (`2x`, `4x`, `2x-tta`…) ao lado das imagens;
  quando já existe, o botão vira "Abrir". A conta fica em
`src/CatgirlStudio.Core/ImageOps.cs`. O Real-ESRGAN é um programa à parte: o app o
procura numa pasta `realesrgan\` ao lado do `.exe` e, no desenvolvimento, em
`tools\bin\realesrgan-ncnn-vulkan\` (fora do git): baixe
`realesrgan-ncnn-vulkan-20220424-windows.zip` da
[release v0.2.5.0](https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.5.0).

## Página web (congelada)

`web/` é a versão de navegador (`page.html`, `app.js`, `engine.js`) com uma cópia
própria do catálogo de antes do OutfitEngine (`web/data/`). Fica como está, para
usar no celular; as novidades vão só para o app.
