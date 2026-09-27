# 08 — Recuperação e Redefinição de Senha

**Status:** ✅ concluído

---

## Objetivo

Permitir que um cliente que esqueceu a senha receba um e-mail com link seguro para
redefinir-la. O mesmo fluxo serve para **definir** a senha de contas criadas via
Google, que nunca tiveram senha.

---

## Endpoints

| Método | Rota | Body | Sucesso |
|---|---|---|---|
| `POST` | `/api/autenticacao/solicitar-reset-senha` | `{ email }` | `200` com mensagem genérica |
| `POST` | `/api/autenticacao/redefinir-senha` | `{ token, novaSenha }` | `200` com tokens e usuário |

---

## Fluxo

```
1. Cliente informa o e-mail
2. Backend procura o usuário
3. Gera token aleatório, guarda apenas o SHA-256 e envia o e-mail
4. Cliente clica no link  →  /redefinir-senha?token=...
5. Backend revalida o hash, a expiração e o uso único
6. Senha é trocada e os tokens são emitidos — o cliente já entra
```

---

## Solicitação

`RecuperacaoDeSenhaService.SolicitarAsync`.

Se o e-mail **não existir**, o método retorna imediatamente, sem e-mail e sem
registro. A resposta HTTP é a mesma nos dois casos:

> Se o e-mail informado existir, você receberá as instruções para redefinir a senha.

Essa mensagem é montada no controller e é intencional: responder diferente para e-mail
existente permitiria enumerar as contas da loja.

### Distinção entre redefinição e definição

```csharp
var temSenha = !string.IsNullOrEmpty(usuario.HashSenha);
var assunto = temSenha ? AssuntoDeRedefinicaoDeSenha : AssuntoDeDefinicaoDeSenha;
```

A mesma rota atende os dois casos, com texto e assunto adaptados. É por isso que
conta Google sem senha consegue ganhar acesso por senha.

### Invalidação de pedidos anteriores

Antes de gravar a nova solicitação, **todas** as recuperações anteriores do usuário
são removidas:

```csharp
var recuperacoesAnteriores = await _contexto.RecuperacoesDeSenha
    .Where(r => r.UsuarioId == usuario.Id).ToListAsync(cancellationToken);
_contexto.RecuperacoesDeSenha.RemoveRange(recuperacoesAnteriores);
```

Assim só o e-mail mais recente funciona. Pedir um novo link invalida o antigo — evita
que um link vazado no e-mail antigo continue válido.

---

## Segurança do token

| Aspecto | Implementação |
|---|---|
| Geração | `RandomNumberGenerator.GetBytes(32)` → base64 URL-safe, 43 caracteres |
| Armazenamento | **Apenas o SHA-256** — o token em si nunca é persistido |
| Validade | 30 minutos (`Frontend:MinutosDeExpiracaoDoTokenDeReset`) |
| Uso | Único — `UtilizadoEm` é gravado após a troca |
| Transporte | Query string, removida da URL pelo frontend após a leitura |

```csharp
private static string GerarToken()
{
    var bytes = RandomNumberGenerator.GetBytes(32);
    return Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

private static string CalcularHashDoToken(string token)
    => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
```

Guardar só o hash significa que, mesmo com acesso ao banco, não é possível forjar um
link de redefinição: a busca é por hash, e o token original nunca foi salvo.
`Replace('+','-')` e `Replace('/','_')` deixam o token seguro dentro de uma URL.

---

## Confirmação

`ConfirmarAsync`:

1. Senha com menos de 6 caracteres → `InvalidOperationException` → `400`.
2. Busca pelo **hash** do token informado.
3. `null`, já utilizado ou expirado → `UnauthorizedAccessException` → `401`.
4. Senha é re-hasheada.
5. As outras recuperações do usuário são removidas.
6. `UtilizadoEm` é gravado.
7. Tokens são emitidos — **o usuário já fica autenticado**.

O passo 7 evita um passo extra: trocar a senha já provou a identidade, então não faz
sentido mandar a pessoa para a tela de login.

---

## Mensagens de erro

| Situação | Mensagem | Status |
|---|---|---|
| Token inválido, expirado ou já usado | "Token de recuperação inválido ou expirado." | `401` |
| Senha curta | "A senha deve ter no mínimo 6 caracteres." | `400` |

---

## E-mail

O template HTML é montado em `ConstruirCorpoDoEmail` e enviado via SMTP. Detalhes de
transporte em [`11-envio-de-email-smtp.md`](11-envio-de-email-smtp.md).

O nome do usuário passa por `WebUtility.HtmlEncode` antes de entrar no HTML:

```csharp
var nomeSeguro = WebUtility.HtmlEncode(nome);
```

Sem isso, um nome com `<` ou `&` quebraria o HTML — e um nome contendo tag seria
interpretado como marcação.

O template é **tabela HTML com estilos inline**, com a identidade visual da marca:

- Logo `LUMI MAKEUP` e o slogan **"Seu brilho começa aqui"**.
- Título e botão conforme o caso: *Redefinição* / *Definição* de senha.
- Botão principal com o link.
- Link em texto puro como alternativa, para cliente de e-mail sem suporte a botão.
- Aviso de validade (30 min) e de uso único.
- Nota de segurança para quem não solicitou.
- Rodapé com `nao-responda@lumimakeup.com.br`.

### Slogan

O template usava o slogan antigo, *"beleza que ilumina"*. Foi alterado para
*"Seu brilho começa aqui"*, alinhando com a identidade visual. O teste automatizado
que ainda validava o texto antigo foi corrigido na mesma alteração — a slogan aparece
no HTML, então o teste existia justamente para protegê-la.

### Link

```
{Frontend:UrlBase}{Frontend:RotaDeRedefinicaoDeSenha}?token={token}
```

Em produção, `UrlBase` é `https://lumimakeup.com.br`; em desenvolvimento,
`http://localhost:4200`. É por isso que a URL do frontend é configuração da API.
