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
| `Cloudinary` | `CloudinaryServiceStub` | ⬜ stub |
| `FocusNfe` | `FocusNfeServiceStub` | ⬜ stub |
| `Baileys` | `WhatsAppServiceStub` | ⬜ stub |
| `Brevo` | — | ⬜ não integrado |

> O bloco de `Brevo` está reservado e não é usado por nenhum código. O e-mail hoje sai
> por SMTP, não pela API do Brevo.

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
