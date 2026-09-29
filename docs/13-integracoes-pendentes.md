# 13 — Integrações Pendentes

**Status:** 🔶 parcial — imagens e IA prontas; WhatsApp e Focus NFe continuam stub

---

## Objetivo

Mapear as integrações externas que a API já referencia mas ainda não implementa, para
que ninguém trate stub como funcionalidade pronta.

---

## Stubs registrados

```csharp
services.AddScoped<IWhatsAppService, WhatsAppServiceStub>();
services.AddScoped<IFocusNfeService, FocusNfeServiceStub>();
```

As interfaces já estão em `Application/Abstractions/IIntegrations.cs` e os registros já
existem em `DependencyInjection.cs`. Só a implementação real falta.

Um stub responde e registra a chamada em log, sem falar com o serviço externo. Isso
mantém a API compilando e o fluxo executável, mas **nada chega ao destino real**.

| Integração | Interface | Usada hoje por | Bloqueia |
|---|---|---|---|
| WhatsApp (Baileys) | `IWhatsAppService` | nada | avisos de pedido |
| Focus NFe | `IFocusNfeService` | nada | emissão de nota fiscal |
| Brevo | — | **nada** | não integrado (ver `02`) |

---

## Imagens de produto — armazenamento local (implementado)

A vitrine deixou de depender de Cloudinary. As imagens ficam no próprio servidor, em uma
pasta **irmã** da aplicação, para que o deploy via FTP nunca as sobrescreva:

```
root/
├── api.lumimakeup.com.br/    → publicação da API
└── imagens/                  → fora do deploy
```

A leitura pública acontece por `imagens.lumimakeup.com.br`, que aponta via DNS para a
pasta `imagens`. O upload passa pela API (para autenticar e validar); a leitura é servida
diretamente pelo servidor web, sem passar pelo app server.

O banco guarda apenas o **caminho relativo** (`produtos/abc123.jpg`), nunca o caminho
absoluto do servidor, então o dado continua válido se a estrutura mudar.

| Camada | Onde |
|---|---|
| Contrato | `IArmazenamentoDeImagens` em `Application/Abstractions/IIntegrations.cs` |
| Implementação | `ArmazenamentoDeImagensLocal` em `Infrastructure/Integrations` |
| CRUD de produtos/categorias | `GestaoDeProdutosService`, `GestaoDeCategoriasService` — ver [`15-gestao-de-produtos.md`](15-gestao-de-produtos.md) |
| CRUD de banners | `GestaoDeBannersService` — ver [`17-banners.md`](17-banners.md) |
| Rotas | `api/admin/produtos`, `api/admin/categorias`, `api/admin/banners` (exigem `SomenteAdministrador`) |

Configuração em `ArmazenamentoDeImagens`:

| Chave | Valor no `appsettings.json` | Papel |
|---|---|---|
| `CaminhoBase` | `../imagens` | **obrigatória**. Relativa ao *content root*, para que a pasta fique **irmã** da aplicação e fora do que o FTP publica |
| `PastaPadrao` | `produtos` | subpasta dentro de `CaminhoBase`, usada por `ArmazenarAsync` |
| `TamanhoMaximoEmBytes` | `5242880` | 5 MB |
| `ExtensoesPermitidas` | `jpg`, `jpeg`, `png` | extensões liberadas |

## `ArmazenarEmPastaAsync`: quando a pasta não é a padrão

`ArmazenarAsync` grava sempre na `PastaPadrao`. Os banners precisavam de uma pasta
própria — `banners/` — para não ficarem misturados com as fotos de produto, e
receberam um método à parte em vez de um parâmetro opcional.

O método novo grava em subpasta informada pelo chamador, e a pasta passa pelo mesmo
`SanearPasta` da padrão: `".."` e barra invertida são recusados, e `"../../segredo"`
vira `segredo` porque o ponto não é caractere aceito. Subpasta interna é aceita
(`banners/2026`).

Subdiretório é criado sob demanda (`Directory.CreateDirectory` antes de gravar), então
`banners/` **não precisa de preparação manual** no servidor de imagens, e herda as
mesmas permissões da raiz que já aceita `produtos/`.

> **Por que método separado e não parâmetro opcional:** acrescentar um parâmetro
> opcional a uma interface já implementada quebra todo chamador de Moq — árvore de
> expressão não aceita argumento omitido (`CS0854`). O nome também fica mais honesto:
> quem chama diz em qual pasta quer gravar.

## A exclusão é melhor esforço

`ExcluirAsync` **não lança** quando o arquivo já sumiu ou quando o caminho
armazenado é inválido. Devolve sucesso nos dois casos.

A referência no banco é o que precisa sair. Sem isso, apagar uma imagem cujo arquivo
tinha sido removido fora do sistema derrubava a requisição com erro — o painel
mostrava falha por um arquivo que não existia mais, e a referência continuava no
banco, que é o oposto do que se queria.

> **Vazio não é "sem configuração".** `ArmazenamentoDeImagensLocal.ResolverRaiz` lança
> ao encontrar a string vazia, e como o registro é `AddScoped` o construtor só roda no
> primeiro uso. A API subia normal, a vitrine funcionava, e a falha aparecia como `500`
> no exato momento de enviar a primeira imagem de produto — o pior tipo de erro, porque
> quem visse aquilo culparia a imagem, e não a configuração. Daí o valor ser explícito
> no arquivo. Confirmado: a partir de `C:\wwwroot\api.lumimakeup.com.br`, `../imagens`
> resolve para `C:\wwwroot\imagens`.

Regras de validação no upload:

- **O formato é decidido pelo conteúdo, não pelo nome.** Os primeiros bytes são
  comparados com as assinaturas de JPEG (`FF D8 FF`) e PNG (`89 50 4E 47 0D 0A 1A 0A`).
  Um `.png` disfarçado é recusado com 400.
- A extensão do arquivo gravado vem do formato detectado, nunca da enviada.
- O nome do arquivo é um GUID gerado pelo servidor. O nome original é saneado e
  guardado em `NomeOriginal` só para exibição.
- Toda gravação passa por `ResolverCaminhoSeguro`, que rejeita `..` e qualquer caminho
  que resolved para fora de `CaminhoBase`.
- Máximo de **3 imagens por produto**.
- Excluir imagem ou produto apaga o arquivo do disco; se o `SaveChanges` falhar depois
  de gravar, o arquivo é removido para não deixar órfão.

> **Obrigatório no servidor:** desabilitar execução de scripts na pasta `imagens`.
> Sem isso, um `.aspx`/`.php` enviado por uma sessão comprometida seria executado.
> Isso é configuração do servidor web, não do código. E as imagens precisam entrar no
> plano de backup: elas não têm mais cópia em serviço de terceiros.

---

## Melhoria de texto com IA (implementado)

No painel de administração, o campo de descrição do produto tem um botão que reescreve
o texto por um modelo de linguagem. O endpoint é `POST api/admin/produtos/texto/melhorar`,
protegido pela policy `SomenteAdministrador`, e devolve
`{ descricaoMelhorada, modeloUsado }`.

| Camada | Onde |
|---|---|
| Contrato | `IMelhoradorDeTextoService` em `Application/Abstractions` |
| Implementação | `MelhoradorDeTextoOpenAiCompativel` em `Infrastructure/Integrations` |

**A chave nunca sai do backend.** O navegador só chama a API; é ela que fala com o
provedor. É o mesmo cuidado do reCAPTCHA, e vale ainda mais aqui porque a chave é
limitada por cota.

**Um formato, muitos provedores.** A implementação fala o formato `chat/completions`
da OpenAI, que Groq, OpenRouter, Cerebras e NVIDIA NIM implementam. Provedor e modelo
são configuração, não código — ver
[`02-configuracao-e-ambiente.md`](02-configuracao-e-ambiente.md).

> **O provedor hoje é o OpenRouter**, com `nvidia/nemotron-3-ultra-550b-a55b:free`. Foi
> uma troca de chave e de URL, sem tocar em código — que é exatamente o que o formato
> único permite. O Groq foi o primeiro (cota de 200K tokens/dia em `openai/gpt-oss-120b`
> e *prompt caching*, que deixa a instrução de sistema longa fora da conta) e continua
> válido como alternativa: tem free tier sem cartão e é só trocar `UrlBase`, `Modelo` e o
> secret `IA_CHAVE`.
>
> O modelo atual é do plano gratuito. Ele serve bem para desenvolvimento, mas provedores
> removem do plano gratuito sem avisar, e o botão paramando com "modelo não existe" em
> produção é o tipo de erro que só alguém conserta percebendo. Para produção, vale um
> modelo pago.
>
> O Gemini foi avaliado antes e ficado de fora: o caminho de Application Default
> Credentials passa pelo Agent Platform, que exige faturamento habilitado no projeto.

### O prompt proíbe a IA de inventar

O ponto mais importante do prompt: reescrever texto de cosmético é uma operação com
risco regulatório. Alegação de benefício sem respaldo é infração de consumo, e o modelo,
treinado para vender, adiciona benefício se perguntado de forma vaga. Por isso as
regras são explícitas:

- não inventar característica, ingrediente, textura ou benefício;
- não usar promessa de resultado garantido nem linguagem de efeito médico;
- não citar porcentagem, selo, aprovação, certificação ou estudo clínico;
- não inventar número de cores, gramas, volume ou duração;
- devolver **somente** a descrição, sem comentário sobre o que mudou.

Ainda assim, o texto gerado entra no formulário **como sugestão editável**, nunca
gravado direto no produto. Revisão humana antes de salvar é requisito, não formalidade.

### Limites

Descrição entre 10 e 4.000 caracteres, nome até 150, `temperatura` 0.3 e teto de 1024
tokens. A temperatura é baixa de propósito: o objetivo é reescrever sem inventar, e
modelo criativo é exatamente o risco aqui.

> A geração passa pela IA, então o texto original e o reescrito saem da sua
> infraestrutura. Para descrição de produto isso não é dado sensível, mas é uma
> decisão consciente do negócio, não um detalhe técnico.



---

## WhatsApp — avisos de pedido

`RegistroWhatsApp` já está no modelo e ligado a `Pedido` por `Cascade`, com
`StatusRegistroWhatsApp` para acompanhar o envio.

Falta o cliente do serviço Baileys, configurado em `ExternalServices:Baileys`
(`UrlBase` apontando para `http://localhost:3001` e `SegredoCompartilhado`). A ideia é
que um serviço Node separado mantenha a sessão do WhatsApp e a API apenas converse com
ele por HTTP.

| Precisa ser definido | Motivo |
|---|---|
| Quais eventos disparam mensagem | Pagamento aprovado, pedido enviado, entregue |
| Texto e template de cada aviso | |
| Política de retry e rate limit | Vários pedidos podem sair juntos |

---

## Focus NFe — nota fiscal

`NotaFiscal` já existe, com `StatusNotaFiscal`, `ReferenciaFocusNfe`, `UrlXml` e
`UrlPdf`. É uma integração de NF-e, com obrigações fiscais e de conformidade.

Falta o cliente da API da Focus, o mapeamento dos dados fiscais do emitente, e a
emissão disparada por evento de pedido. `StatusNotaFiscal.Pendente` já é o padrão da
entidade, então o fluxo foi pensado desde o início.

> Por envolver responsabilidade fiscal, essa integração deve ser fechada com
> contador antes de ir para produção.

---

## Ordem sugerida

1. ~~**Imagens de produto**~~ — concluído (armazenamento local, ver acima).
2. ~~**Melhoria de texto com IA**~~ — concluído (ver acima).
3. **Checkout e pedidos** — modelo de endereço já pronto (ver `05`); é o que gera
   receita.
4. **WhatsApp** — curto, e melhora a percepção do cliente sobre o pedido.
5. **Focus NFe** — por último, por envolver conformidade.

---

## Como verificar o que é stub

Nomes terminados em `Stub` são implementações fictícias:

```
EmailSenderStub.cs
FocusNfeServiceStub.cs
WhatsAppServiceStub.cs
```

A exceção é `EmailSenderStub`: ele é um **fallback real**, escolhido por `DependencyInjection`
quando o SMTP não está configurado (ver [`11-envio-de-email-smtp.md`](11-envio-de-email-smtp.md)).
