# 10 — Validação de reCAPTCHA

**Status:** ✅ concluído

---

## Objetivo

Impedir que robôs criem contas em massa na página de cadastro, validando o token do
reCAPTCHA v3 **no servidor** antes de gravar qualquer usuário.

---

## Fluxo

```
1. Frontend carrega reCAPTCHA v3 e pede o token ao Google
2. Frontend envia o token em RequisicaoDeRegistro.TokenRecaptcha
3. Backend troca o token pela Google reCAPTCHA "siteverify"
4. Só com success = true o cadastro segue
```

O reCAPTCHA v3 **não exibe desafio**: ele devolve um score de 0.0 a 1.0 e a decisão é
da aplicação.

---

## Por que validar no backend

Validar apenas no frontend não protege nada. O token fica em uma requisição que o
atacante pode repetir sem passar pela interface. A chamada `siteverify` acontece
exclusivamente no servidor, com a chave secreta que **nunca vai para o navegador**:

```csharp
var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["secret"] = _opcoes.ChaveSecreta,
    ["response"] = token
});
```

A chave secreta está em `ExternalServices:Recaptcha:ChaveSecreta` no
`appsettings.Development.json`, que é ignorado pelo git.

---

## Configuração

```csharp
services.Configure<RecaptchaOptions>(configuration.GetSection("ExternalServices:Recaptcha"));

public sealed class RecaptchaOptions
{
    public string ChaveSecreta { get; set; } = string.Empty;
    public double LimiteDeScore { get; set; } = 0.5;
}
```

`LimiteDeScore` padrão é **0.5**: acima disso a requisição é humana ACEITA. Abaixo, é
recusada.

---

## `RecaptchaValidator.ValidarTokenAsync`

| Situação | Resultado |
|---|---|
| `ChaveSecreta` vazia | `true` + aviso no log |
| Token ausente ou vazio | `false` |
| Resposta HTTP não bem-sucedida | `false` |
| `success = false` | `false` |
| `score` abaixo do limite | `false` |
| `success = true` e score ok | `true` |

### Tolerância quando não configurado

```csharp
if (string.IsNullOrWhiteSpace(_opcoes.ChaveSecreta))
{
    _logger.LogWarning("RecaptchaValidator: ChaveSecreta não configurada — aceitando registro sem validação.");
    return true;
}
```

Sem chave, a validação é **ignorada** e o cadastro funciona. Isso permite rodar a API
e os testes em máquina sem credencial do Google. O aviso no log deixa a condição
visível.

> Esse atalho só é seguro porque a chave é obrigatória em produção. Em outro
> ambiente, um `throw` seria mais garantido.

### Score ausente

```csharp
if (resultado.Score is not null && resultado.Score < _opcoes.LimiteDeScore)
```

O `Score` é `double?`. Quando o Google não devolve score, a comparação não acontece e
o registro é aceito. Só reprova com score **presente e baixo**.

### Diagnóstico

Todas as rejeições registram motivo, mas nunca retornam o detalhe ao cliente:

```csharp
_logger.LogWarning("RecaptchaValidator: validação falhou ({Detalhes}).", detalhes);
```

O `catch` não existe aqui — diferente do ViaCEP, uma falha do Google na validação
**reprova** o cadastro. É o comportamento seguro: na dúvida, não cria a conta.

O frontend exibe apenas "Falha na validação do reCAPTCHA.", sem explicar o score. Dizer
que o score foi baixo ajudaria quem está sendo bloqueado legitimamente.

---

## No fluxo de cadastro

`CadastrarAsync` chama o validador **antes de qualquer consulta ao banco**:

```csharp
var recaptchaValido = await _recaptchaValidator.ValidarTokenAsync(
    requisicao.TokenRecaptcha ?? string.Empty, cancellationToken);
if (!recaptchaValido)
{
    throw new InvalidOperationException("Falha na validação do reCAPTCHA.");
}
```

Duas consequências:

1. **Sem enumeração de contas.** Se a consulta ao banco viesse antes, a resposta 409
   "E-mail já cadastrado" transformaria o cadastro em oráculo de existência de
   e-mails, ignorando o captcha por completo.
2. **Custo de chamada ao Google evitado** para requisições claramente inválidas.

O `?? string.Empty` evita `NullReferenceException` quando o campo não vem no JSON —
um corpo `{ "email": "...", "senha": "..." }` sem o token cai no tratamento de "token
ausente" e devolve `false`, em vez de quebrar.

`LimiteDeScore` não aparece no `appsettings.json` versionado; usa o padrão de 0.5 da
classe.
