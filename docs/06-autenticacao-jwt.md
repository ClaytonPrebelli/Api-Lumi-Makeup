# 06 — Autenticação JWT

**Status:** ✅ concluído

---

## Objetivo

Autenticar o cliente com um par de tokens — **access** de curta duração e **refresh**
de longa duração — e proteger as rotas que exigem usuário conectado.

---

## Fluxo

```
POST /api/autenticacao/entrar
  ↓
200 { tokenAcesso, tokenRefresh, usuario }
  ↓
frontend guarda os dois tokens
  ↓
chama a API com  Authorization: Bearer {tokenAcesso}
  ↓
access expira em 60 min →  POST /api/autenticacao/renovar com o refresh
  ↓
refresh expira em 7 dias →  o cliente precisa entrar de novo
```

---

## Os dois tokens

`JwtTokenService.GerarTokens` emite dois JWTs assinados com **HMAC-SHA256**, a partir
da mesma chave simétrica.

### Access token

| Claim | Valor |
|---|---|
| `sub` | `usuario.Id` |
| `name` | `usuario.Nome` |
| `email` | `usuario.Email` |
| `role` | `usuario.Papel` |
| `jti` | GUID novo |
| `typ` | `"access"` |

Validade: **60 minutos** (`Jwt:MinutosDeExpiracao`).

### Refresh token

| Claim | Valor |
|---|---|
| `sub` | `usuario.Id` |
| `jti` | GUID novo |
| `typ` | `"refresh"` |

Validade: **7 dias** (`Jwt:DiasDeExpiracaoDoRefresh`).

O refresh carrega `sub`, `email` e `jti` — sem nome nem papel. O e-mail está lá
para amarrar a renovação à pessoa: se o id for reaproveitado por outra conta,
a renovação morre em vez de emitir tokens para a pessoa errada. Levar menos
dados reduz a exposição se ele for interceptado.

A claim `typ` é a distinção entre os dois. Ela impede que um refresh token seja
aceito como se fosse access: o middleware de autorização rejeita token sem `role`, e
`ObterIdDeUsuarioDoTokenRefresh` só devolve o `sub` quando `typ == "refresh"`.

---

## Validação

`TokenValidationParameters` em `Program.cs` valida tudo — emissor, audiência,
validade e assinatura:

```csharp
ValidateIssuer = true,
ValidateAudience = true,
ValidateLifetime = true,
ValidateIssuerSigningKey = true,
ValidIssuer = opcoesJwt.Emissor,
ValidAudience = opcoesJwt.Audiencia,
IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoesJwt.Segredo))
```

Os quatro `true` são explícitos porque o padrão do ASP.NET Core historicamente foi
`false` em alguns deles. Desligar `ValidateIssuer` permitiria aceitar um token
assinado com a mesma chave mas emitido para outro sistema.

`NameClaimType` e `RoleClaimType` são realinhados para `ClaimTypes.Name` e
`ClaimTypes.Role`, de modo que `[Authorize(Roles = "Administrador")]` funcione
diretamente com a claim `role`.

---

## Sessão amarrada à pessoa

Assinatura válida não basta: se o banco for recriado, o id do token pode passar
a ser de outra pessoa, e um pedido feito com a sessão antiga cairia na conta
errada. Por isso `OnTokenValidated` (`ValidacaoDeSessao.AoTokenValidado`)
confere **id + e-mail** do token contra o banco a cada request em rota
`[Authorize]`. Usuário inexistente ou e-mail diferente derruba com `401`
"Sessão inválida. Entre de novo." — e o front desloga pelo caminho que já
existe. O e-mail nunca muda após o cadastro, então conta válida não cai.

---

## Renovação

```http
POST /api/autenticacao/renovar
{ "tokenRefresh": "..." }
```

`ObterIdDeUsuarioDoTokenRefresh` valida assinatura, emissor, audiência e vida útil
com `ClockSkew` de **2 minutos** — tolerância para pequeno desvio de relógio entre
servidores. Se qualquer validação falhar, retorna `null`.

A partir do `sub`, o usuário é recarregado e um par novo de tokens é emitido. Como a
emissão busca o usuário no banco a cada renovação, uma conta removida ou bloqueada
não continua renovando.

| Situação | Resposta |
|---|---|
| Refresh válido | `200` com tokens novos |
| Refresh inválido, expirado ou de tipo errado | `401` "Token de atualização inválido ou expirado." |
| Usuário não encontrado | `401` "Usuário não encontrado." |
| E-mail do refresh diferente do banco, ou refresh antigo sem e-mail | `401` "Sessão inválida. Entre de novo." |

> O refresh token não é revogado individualmente. Trocar a senha invalida as
> renovações na prática porque o hash muda, mas o token ainda é criptograficamente
> válido até a expiração. Um blacklist exigiria armazenar o `jti` — não implementado.

---

## Rotas protegidas

| Método | Rota | Acesso |
|---|---|---|
| `GET` | `/api/autenticacao/eu` | autenticado |
| `POST` | `/api/autenticacao/completar-perfil` | autenticado |

O id do usuário vem do token, **nunca do body**:

```csharp
var usuarioId = ObterIdDoUsuario();
```

`ObterIdDoUsuario` lê a claim `NameIdentifier` e faz `long.TryParse`. Se o parse
falhar — token válido mas com `sub` inesperado — devolve `401`. Sem essa checagem, um
`sub` inválido propagaria um `FormatException` e resultaria em `500`.

Isso elimina a possibilidade de um cliente consultar ou editar o perfil de outro
usuário, porque o id não vem da requisição.

---

## Onde o hash da senha fica

Senha nunca é guardada em texto. `PasswordHasher<Usuario>` (ASP.NET Core Identity)
gera hash com salt por senha:

```csharp
usuario.HashSenha = _passwordHasher.HashPassword(usuario, requisicao.Senha);
```

A verificação é feita com `VerifyHashedPassword`. Usuários criados via Google começam
com `HashSenha` nulo — é assim que o sistema distingue "conta sem senha" de "senha
vazia", o que habilita o e-mail de *definição* de senha descrito em
[`08-recuperacao-de-senha.md`](08-recuperacao-de-senha.md).
