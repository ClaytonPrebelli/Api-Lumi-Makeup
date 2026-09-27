# 11 — Envio de E-mail via SMTP

**Status:** ✅ concluído

---

## Objetivo

Enviar e-mails transacionais reais — hoje, o de redefinição de senha — com a identidade
visual da marca, sem acoplar o serviço a um provedor específico.

---

## Camadas

O envio é dividido em duas responsabilidades, e essa separação é o ponto principal do
desenho:

| Camada | Tipo | Responsabilidade |
|---|---|---|
| `SmtpEmailSender` | `IEmailSender` | Montar a mensagem a partir das opções |
| `EnviadorDeEmailSmtpViaClienteSmtp` | `IEnviadorDeEmailSmtp` | Transportar a mensagem pela rede |

O serviço de recuperação de senha depende só de `IEmailSender`. Trocar SMTP por um
serviço HTTP de e-mail significa escrever um novo `IEmailSender` — nada em
`RecuperacaoDeSenhaService` muda.

---

## Escolha de implementação

A interface interna `IEnviadorDeEmailSmtp` recebe `System.Net.Mail.MailMessage`, e a
implementação converte para `MimeMessage` antes de enviar pelo MailKit. É uma
ponte proposital: `MailMessage` é o tipo do framework, então cada novo serviço pode
usá-lo sem conhecer MailKit.

```
SmtpOptions ─┐
             ├─→ SmtpEmailSender ─→ MailMessage ─→ IEnviadorDeEmailSmtp ─→ MimeMessage ─→ MailKit ─→ SMTP
Mensagem ────┘
```

---

## Configuração

```csharp
public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Porta { get; set; } = 465;
    public string Usuario { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public string Remetente { get; set; } = string.Empty;
    public string NomeDoRemetente { get; set; } = string.Empty;
    public bool UsarSsl { get; set; } = true;
}
```

Lida de `ExternalServices:Smtp`. Porta **465** com `UsarSsl = true` é SMTP
implícito sobre TLS — a conexão já é criptografada desde o início, sem o upgrade
`STARTTLS` da porta 587.

---

## Escolha da segurança

```csharp
var seguranca = (_opcoes.UsarSsl, _opcoes.Porta) switch
{
    (true, < 587) => SecureSocketOptions.SslOnConnect,
    (true, _)     => SecureSocketOptions.StartTls,
    _             => SecureSocketOptions.None
};
```

| `UsarSsl` | Porta | Modo |
|---|---|---|
| `true` | menor que 587 | `SslOnConnect` — TLS desde o handshake (465) |
| `true` | 587 ou maior | `StartTls` — sobe para TLS após o greeting |
| `false` | qualquer | `None` |

O switch explícito evita a falha clássica de conectar na 465 esperando `STARTTLS` que
nunca chega, porque o servidor já espera TLS.

O `Timeout` é de 15 segundos — sem ele, um servidor SMTP que não responde trava a
requisição até o timeout padrão do `HttpClient`/sistema.

---

## Fallback para stub

```csharp
var smtpConfigurado = !string.IsNullOrWhiteSpace(configuration["ExternalServices:Smtp:Host"]);
if (smtpConfigurado)
{
    services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    services.AddScoped<IEmailSender, EmailSenderStub>();
}
```

Sem `Host`, a API registra `EmailSenderStub`, que registra a mensagem em log em vez de
enviar. Ninguém recebe e-mail, mas a aplicação sobe e o fluxo funciona de ponta a
ponte — o que permite testar o reset de senha inteiro sem servidor de e-mail.

É o mesmo padrão de `RecaptchaValidator` e vale pela mesma razão: a API precisa ser
executável em uma máquina de desenvolvimento limpa.

---

## Falha de envio não derruba o fluxo

```csharp
catch (Exception excecao)
{
    _logger.LogError(excecao, "Falha ao enviar e-mail via SMTP. Para={Destino} Assunto={Assunto}", destino, assunto);
}
```

O `SmtpEmailSender` **não relança**. Um SMTP fora do ar não pode impedir que o token de
redefinição seja gerado e gravado — do contrário o cliente ficaria sem e-mail e sem
token válido, sem nenhuma forma de se recuperar.

A exceção é registrada com `LogError` e o fluxo segue. O e-mail perdido é aceitável;
a conta trancada não.

No `EnviadorDeEmailSmtpViaClienteSmtp`, o `catch` faz o oposto: desconecta sem `quit` e
relança, para o socket não ficar pendurado. Quem decide absorver o erro é a camada de
cima.

---

## Cobertura

`EnviarAsync` e `ConstruirMime` estão marcados com `[ExcludeFromCodeCoverage]`:

```csharp
[ExcludeFromCodeCoverage]
public async Task EnviarAsync(MailMessage mensagem, CancellationToken cancellationToken = default)
```

`ConstruirMime` só é alcançável a partir de `EnviarAsync`. Sem a mesma marcação, ela
aparecia como código nunca coberto — 11 linhas que exigem uma conexão SMTP real para
ser exercitadas, sem verificar nada de útil.

> O `.gitignore` já cobre `coverage.*.xml` e `coverage.json`, então relatórios de
> cobertura gerados localmente não entram no repositório.
