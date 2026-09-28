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
as credenciais de SMTP, a chave secreta do reCAPTCHA, o segredo do cliente Google e a
senha do administrador de seed. Nada disso vai para o git.

> Ao adicionar uma chave nova em `appsettings.Development.json`, não a copie para
> `appsettings.json`. Deixe a chave vazia no arquivo versionado.

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

Se ambos os campos estiverem preenchidos, o seed roda na inicialização. Sem eles, um
aviso é registrado e nada acontece.

### `ExternalServices`

| Chave | Consumida por | Estado |
|---|---|---|
| `Smtp` | `SmtpEmailSender` | ✅ em uso |
| `Recaptcha` | `RecaptchaValidator` | ✅ em uso |
| `Google` | `AutenticacaoGoogleService` | ✅ em uso |
| `Gemini` | `MelhoradorDeTextoGemini` | ✅ em uso — ver abaixo |
| `FocusNfe` | `FocusNfeServiceStub` | ⬜ stub |
| `Baileys` | `WhatsAppServiceStub` | ⬜ stub |
| `Brevo` | — | ⬜ não integrado |

> O bloco de `Brevo` está reservado e não é usado por nenhum código. O e-mail hoje sai
> por SMTP, não pela API do Brevo.

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

## `ExternalServices:Gemini`

Usada por `MelhoradorDeTextoGemini` para reescrever a descrição de um produto.

| Chave | Padrão | Papel |
|---|---|---|
| `Chave` | `""` | Chave da AI Studio. Se preenchida, tem precedência |
| `Projeto` | `""` | ID do projeto Google Cloud, usado no caminho do Agent Platform |
| `Local` | `global` | Região do endpoint (`global`, `us-central1`, …) |
| `Modelo` | `gemini-3.5-flash` | Modelo chamado |
| `EndpointDaAiStudio` | `https://generativelanguage.googleapis.com/` | Base do modo chave |
| `EndpointDoVertex` | `https://aiplatform.googleapis.com/` | Base do modo ADC |
| `TimeoutEmSegundos` | `30` | Tempo limite da chamada |

Há **dois modos de autenticação**, escolhidos automaticamente:

| Modo | Quando | URL chamada | Autenticação |
|---|---|---|---|
| Chave da AI Studio | `Chave` preenchida | `generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent` | header `x-goog-api-key` |
| Credenciais padrão | `Chave` vazia e `Projeto` preenchido | `aiplatform.googleapis.com/v1/projects/{projeto}/locations/{local}/publishers/google/models/{modelo}:generateContent` | `Authorization: Bearer` via ADC |

No modo ADC, o token vem de `IProvedorDeTokenDoGoogle`, que usa
`GoogleCredential.GetApplicationDefaultAsync()`. Ele lê, nesta ordem:

1. `GOOGLE_APPLICATION_CREDENTIALS` apontando para um JSON de service account;
2. o arquivo de credenciais gerado por `gcloud auth application-default login`;
3. as credenciais do servidor, quando a API roda em Google Cloud.

A `GoogleCredential` é carregada uma vez e reaproveitada; a renovação do token fica
com a biblioteca.

> **Atenção ao custo:** o modo ADC passa pelo Gemini Enterprise Agent Platform, que
> **exige faturamento habilitado** no projeto — não é o free tier sem cobrança da AI
> Studio. Se a prioridade for não ligar faturamento, use o modo chave.
>
> **Atenção ao deploy:** ADC só existe onde foi configurado. Em desenvolvimento, com
> `gcloud auth application-default login` feito, funciona. Na hospedagem, será preciso
> definir `GOOGLE_APPLICATION_CREDENTIALS` para o JSON de uma service account com o
> papel que permite chamar o modelo.

Erros do Google são traduzidos para mensagens que o admin entende, sem repassar o
corpo bruto da resposta (que pode conter detalhe interno da conta).

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
