# 02 — Configuração e Ambiente

**Status:** ✅ concluído

---

## Objetivo

Centralizar toda a configuração externa da API em um lugar previsível, com nomes em
português e seções bem definidas, e garantir que nenhum segredo entre no repositório.

---

## Arquivos de configuração

| Arquivo | Versionado | Uso |
|---|---|---|
| `appsettings.json` | sim | Valores padrão, sem segredos — chaves vazias |
| `appsettings.Development.json` | **não** | Valores reais de desenvolvimento |

O `appsettings.json` versionado tem **todos os segredos vazios**. Ele serve como
documentação do formato: mostra exatamente quais chaves existem, sem vazar nada.

### Segredos no ambiente local

O `.gitignore` protege os arquivos com configuração real:

```gitignore
# Local secrets - NUNCA versionar
src/LumiMakeup.Api/appsettings.Development.json
src/LumiMakeup.Api/appsettings.Local.json
src/LumiMakeup.Api/appsettings.Production.json
src/LumiMakeup.Api/appsettings.*.local.json
```

O `appsettings.Development.json` local guarda a connection string, o segredo do JWT,
as credenciais de SMTP, a chave secreta do reCAPTCHA, o segredo do cliente Google e o
segredo do Baileys. Nada disso vai para o git.

> Ao adicionar uma chave nova em `appsettings.Development.json`, não a copie para
> `appsettings.json`. Deixe a chave vazia no arquivo versionado.
>
> **Esta regra foi quebrada uma vez.** `ExternalServices:Baileys:SegredoCompartilhado`
> está com valor no arquivo versionado. É o exemplo do que acontece quando a regra é
> ignorada: corrigir exige rotacionar, e não só apagar.

---

## Seções de configuração

### `ConnectionStrings:ConexaoPadrao`

```json
"ConnectionStrings": { "ConexaoPadrao": "" }
```

Obrigatória. `AddInfrastructure` lança `InvalidOperationException` se estiver ausente.
`CharSet=utf8mb4` é necessário para os acentos e símbolos do e-mail institucional.

### `Cors:OrigensPermitidas`

```json
"Cors": { "OrigensPermitidas": [ "http://localhost:4200", "https://lumimakeup.com.br" ] }
```

### `Jwt`

```json
"Jwt": {
  "Emissor": "api.lumimakeup.com.br",
  "Audiencia": "lumimakeup.com.br",
  "MinutosDeExpiracao": 60,
  "DiasDeExpiracaoDoRefresh": 7
}
```

`Segredo` fica vazio no arquivo versionado. Se estiver vazio, o bloco de autenticação
**não é registrado** — a API sobe sem proteção. Detalhes em
[`01-fundacao-da-api.md`](01-fundacao-da-api.md).

### `Frontend`

```json
"Frontend": {
  "UrlBase": "https://lumimakeup.com.br",
  "RotaDeRedefinicaoDeSenha": "/redefinir-senha",
  "MinutosDeExpiracaoDoTokenDeReset": 30
}
```

Configuração do **frontend** dentro da API. É necessária porque o backend monta o link
do e-mail de redefinição de senha e precisa saber a URL pública da loja. Em
desenvolvimento, `UrlBase` é `http://localhost:4200`.

### `Autenticacao:SeedAdministrador`

```json
"Autenticacao": { "SeedAdministrador": { "Email": "...", "Senha": "..." } }
```

> **Seção sem efeito.** O seed não roda mais na inicialização — o `Program.cs` não chama
> o `DatabaseSeeder`. Ela fica aqui porque a chave continua sendo lida por quem for
> usar o seeder à mão, e porque o `.gitignore` já separava `appsettings.Production.json`
> por causa dela. Ver [`01-fundacao-da-api.md`](01-fundacao-da-api.md).

### `ExternalServices`

| Chave | Consumida por | Estado |
|---|---|---|
| `Smtp` | `SmtpEmailSender` | ✅ em uso |
| `Recaptcha` | `RecaptchaValidator` | ✅ em uso |
| `Google` | `AutenticacaoGoogleService` | ✅ em uso |
| `Gemini` | — | removido, ver `13` |
| `Ia` | `MelhoradorDeTextoOpenAiCompativel` | ✅ em uso — ver abaixo |
| `FocusNfe` | `FocusNfeServiceStub` | ⬜ stub |
| `Baileys` | `BaileysWhatsAppService` ou `WhatsAppServiceStub` | ✅ em uso — ver abaixo |
| `Brevo` | — | ⬜ não integrado |

> O bloco de `Brevo` está reservado e não é usado por nenhum código. O e-mail hoje sai
> por SMTP, não pela API do Brevo.

### `ExternalServices:Baileys`

```json
"Baileys": {
  "Habilitado": true,
  "IniciarProcesso": false,
  "UrlBase": "http://localhost:3001",
  "SegredoCompartilhado": "",
  "Porta": 3001,
  "TimeoutDoEnvioEmSegundos": 15
}
```

| Chave | Papel |
|---|---|
| `Habilitado` | **`true` registra o cliente real; `false` registra o stub** |
| `IniciarProcesso` | `true` sobe o Node como hosted service. `false` em produção |
| `UrlBase` | Base do Node. Vem do secret `BAILEYS_URL_BASE` |
| `SegredoCompartilhado` | **segredo.** Vem do secret `BAILEYS_SEGREDO` |
| `Porta` | Só usada quando `UrlBase` está vazia |

Detalhamento em [`18-whatsapp-e-baileys.md`](18-whatsapp-e-baileys.md).

> ⚠️ **`SegredoCompartilhado` está hoje com valor literal em `appsettings.json`, que é
> versionado.** Isso viola a regra deste próprio documento — a chave deveria estar vazia
> no arquivo do git, e o valor vir do secret do repositório.
>
> São **dois** passos, não um: mover para o secret **e** rotacionar no Node. O valor já
> está no histórico do Git, então mudar o lugar não o torna secreto de novo — ele
> continua válido para quem leu. Enquanto a rotação não acontecer, trate o Node como
> exposto.

---

## `ArmazenamentoDeImagens`

Seção de nível superior (fora de `ExternalServices`) usada por
`ArmazenamentoDeImagensLocal`.

| Chave | Padrão | Papel |
|---|---|---|
| `CaminhoBase` | `""` | **Obrigatória.** Raiz onde as imagens são gravadas |
| `PastaPadrao` | `produtos` | Subpasta criada dentro de `CaminhoBase` |
| `TamanhoMaximoEmBytes` | `5242880` | 5 MB por arquivo |
| `ExtensoesPermitidas` | `jpg`, `jpeg`, `png` | Extensões liberadas |

`CaminhoBase` absoluto é o esperado em produção — por exemplo, a pasta `imagens` que é
irmã da pasta da aplicação. Se for relativo, é resolvido a partir do *content root*
(útil em desenvolvimento: `../imagens`).

> Com `CaminhoBase` vazio, `ArmazenamentoDeImagensLocal` lança na construção, e a API
> não sobe. É proposital: a configuração ausente aparece no deploy, não no primeiro
> upload de um cliente.

---

## `ExternalServices:Ia`

Usada por `MelhoradorDeTextoOpenAiCompativel` para reescrever a descrição de um produto.

| Chave | Padrão | Papel |
|---|---|---|
| `Chave` | `""` | Chave do provedor. Sem ela, o botão informa que a IA não está configurada |
| `UrlBase` | `https://openrouter.ai/api/v1` | Base compatível com a API da OpenAI |
| `Modelo` | `nvidia/nemotron-3-ultra-550b-a55b:free` | Modelo chamado |
| `Temperatura` | `0.3` | Baixa de propósito, para o modelo não inventar |
| `MaximoDeTokens` | `1024` | teto da resposta |
| `TimeoutEmSegundos` | `45` | Tempo limite da chamada |

A chamada é sempre `POST {UrlBase}/chat/completions`, com a chave em
`Authorization: Bearer`.

> **`UrlBase` e `Chave` precisam concordar com o provedor.** A `Chave` é secret e vem do
> GitHub, pelo secret `IA_CHAVE`; `UrlBase` e `Modelo` não são segredo e vivem aqui. Se os
> dois divergirem, a chave de um provedor é enviada para a URL de outro e volta `401` —
> que é o que aconteceu quando a chave era da OpenRouter e o padrão apontava para o Groq.
> A chave de um provedor **não** funciona em outro, mesmo com o mesmo formato de API.

### Trocar de provedor

Groq, OpenRouter, Cerebras e NVIDIA NIM falam o mesmo formato. Trocar de fornecedor é
mudar **duas linhas de configuração** neste arquivo, mais trocar o valor do secret
`IA_CHAVE`, sem tocar em código:

```json
"Ia": {
  "Chave": "",
  "UrlBase": "https://api.groq.com/openai/v1",
  "Modelo": "openai/gpt-oss-120b"
}
```

> **Atenção:** nomes de modelo mudam com frequência, e provedores removem modelos do
> plano gratuito sem avisar. O modelo atual é do plano gratuito do OpenRouter (sufixo
> `:free`), o que serve para desenvolvimento e pode sumir a qualquer momento. Para
> produção, um modelo pago é a diferença entre um botão que funciona e um botão que
> precisa de alguém para arrumá-lo. Por isso o modelo fica em configuração e nunca fixo
> no código — a mensagem de erro traduz "modelo não existe" para o admin, que é o erro
> mais provável de um botão parado.

> **Atenção à temperatura:** nunca use `0`. O Groq converte silenciosamente para
> `1e-8`, e há teste garantindo que o valor enviado fica acima de zero.

Erros do provedor são traduzidos para mensagens que o admin entende, sem repassar o
corpo bruto da resposta, que pode conter detalhe interno da conta.

---

## `NotificacoesDePedido`

Seção de nível superior usada por `NotificacoesDePedidoOptions`, que alimenta o
`NotificadorDePedido` e o `MontadorDeMensagemDePedido`.

| Chave | Valor | Papel |
|---|---|---|
| `EmailDaAdministradora` | `use.lumimakeup@gmail.com` | quem recebe o aviso de novo pedido |
| `WhatsAppDaAdministradora` | `5515991475568` | número da loja nos avisos |
| `NomeDaLoja` | `Lumi Makeup` | valor do marcador `{Loja}` |
| `MensagemInicialWhatsAppCliente` | frase padrão | **fallback** quando a tabela está vazia |
| `AvisarPorWhatsApp` | `true` | liga o envio ao cliente |

> **`WhatsAppDaAdministradora` não é o número que envia.** Quem envia é o número
> pareado na sessão do Baileys. Os dois campos são independentes, e confundi-los produz
> um alerta que chega no número certo e responde do número errado.
>
> `MensagemInicialWhatsAppCliente` só é lida enquanto ninguém salvou uma frase pelo
> painel. Depois disso o valor vem de `configuracao_whatsapp`. Ver
> [`18-whatsapp-e-baileys.md`](18-whatsapp-e-baileys.md).

---

## `ExecutarServidor`

```csharp
if (builder.Configuration.GetValue<bool>("ExecutarServidor", defaultValue: true))
{
    app.Run();
}
```

Com a chave ausente ou `false`, o `WebApplication` é construído e configurado, mas
`app.Run()` não é chamado. O `Program` expõe `public partial class Program`, então os
testes de integração sobem a aplicação com `WebApplicationFactory<Program>` sem
ocupar a porta.

---

## Perfis de ambiente

`builder.Environment.IsDevelopment()` controla o Swagger e, indiretamente, qual
`appsettings.*.json` é sobreposto. Em produção o Swagger fica **desligado** — ele fica
disponível apenas com o perfil de desenvolvimento.
