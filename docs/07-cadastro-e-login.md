# 07 — Cadastro, Login e Login Social

**Status:** ✅ concluído

---

## Objetivo

Permitir que uma pessoa crie conta e entre na loja, por e-mail/senha ou pelo Google,
com validação de reCAPTCHA no cadastro e normalização consistente de e-mail.

---

## Endpoints

| Método | Rota | Body | Sucesso | Erros |
|---|---|---|---|---|
| `POST` | `/api/autenticacao/cadastrar` | `RequisicaoDeRegistro` | `200` tokens + usuário | `409` |
| `POST` | `/api/autenticacao/entrar` | `RequisicaoDeLogin` | `200` tokens + usuário | `401` |
| `POST` | `/api/autenticacao/google` | `RequisicaoDeLoginGoogle` | `200` tokens + usuário | `401` / `409` |
| `POST` | `/api/autenticacao/completar-perfil` | `RequisicaoDeCompletarPerfil` | `200` usuário | `400` |

---

## Normalização de e-mail

Toda entrada de e-mail passa por `Trim()` + `ToLowerInvariant()`:

```csharp
var email = requisicao.Email.Trim().ToLowerInvariant();
```

Aplicado de forma idêntica no cadastro, no login, no login Google e na solicitação de
reset. Como `usuarios.Email` tem índice único, normalizar na entrada evita dois
contas para a mesma pessoa — `Joana@Email.com` e `joana@email.com` são a mesma.

`ToLowerInvariant()` — e não `ToLower()` — porque o comportamento de caixa
depende da localidade do servidor. Em línguas como o turco, `ToLower()` produz
resultados inesperados.

---

## Cadastro

`AutenticacaoService.CadastrarAsync`, em ordem:

1. **Valida o reCAPTCHA.** Falha → `InvalidOperationException` → `409`.
2. **Normaliza o e-mail.**
3. **Verifica duplicidade.** Já existe → `InvalidOperationException("E-mail já cadastrado.")` → `409`.
4. **Valida a senha.** Menor que 6 caracteres → `InvalidOperationException` → `409`.
5. **Cria o usuário** com `Papel = PapelUsuario.Cliente` e `CriadoEm = DateTime.UtcNow`.
6. **Gera o hash** da senha.
7. **Salva e já devolve os tokens** — não é preciso chamar `/entrar` em seguida.

> A ordem importa: o reCAPTCHA é validado **antes** de qualquer consulta ao banco.
> Sem essa guarda, um atacante conseguiria enumerar e-mails cadastrados usando a
> página de cadastro como oráculo, ignorando completamente o captcha.

O papel é sempre `Cliente`. Ninguém se cadastra como administrador pela API — o
`DatabaseSeeder` cria o administrador, mas só quando alguém o chama: hoje **ninguém**
chama, porque o banco de produção é o mesmo do desenvolvimento e o admin já existe lá.
Ver [`01-fundacao-da-api.md`](01-fundacao-da-api.md).

---

## Login

`EntrarAsync` busca o usuário pelo e-mail normalizado e, se não existir **ou** não
tiver senha, responde `401`:

```csharp
if (usuario is null || string.IsNullOrEmpty(usuario.HashSenha))
{
    throw new UnauthorizedAccessException("E-mail ou senha inválidos.");
}
```

A mesma mensagem para e-mail inexistente e senha errada — não revelar quais contas
existem é requisito, não detalhe de implementação. A verificação de senha usa
`VerifyHashedPassword`, e só `PasswordVerificationResult.Failed` é rejeitado.

> O `||` entre `usuario is null` e `HashSenha` vazio é proposital: uma conta criada
> via Google não pode entrar por senha, mas também não deve receber uma mensagem
> diferente da de um e-mail inexistente.

---

## Login social com Google

`AutenticacaoGoogleService` troca o `tokenId` do Google pela identidade e devolve
`IdGoogle`, `Email` e `Nome`. O serviço valida o token diretamente com o Google —
**a API não confia no que o frontend envia**.

O fluxo tem três desfechos:

| Situação | Ação |
|---|---|
| Nenhum usuário com esse `IdGoogle` nem esse e-mail | Cria conta nova com `Papel = Cliente` |
| Usuário existe pelo e-mail, sem `IdGoogle` | **Vincula** o `IdGoogle` à conta existente |
| Usuário já vinculado | Apenas autentica |

O caso intermediário é o que faz o produto se comportar bem: quem já tinha conta por
e-mail e depois entra pelo Google **mantém a mesma conta**, com o histórico de
pedidos e endereços intactos. Sem ele, a pessoa teria duas contas e perderia o
histórico.

Quando o Google não devolve o nome, o e-mail é usado como nome provisório:

```csharp
Nome = string.IsNullOrWhiteSpace(dados.Nome) ? email : dados.Nome,
```

O nome real é corrigido depois em `completar-perfil`.

---

## Completar perfil

Rota autenticada. Atualiza nome, CPF e telefone, e — se um endereço for enviado —
substitui o endereço padrão do usuário.

### CPF

```csharp
var cpf = new string(requisicao.Cpf.Where(char.IsDigit).ToArray());
if (cpf.Length != 11)
{
    throw new InvalidOperationException("CPF inválido.");
}
```

A pontuação é descartada e o tamanho é conferido: `123.456.789-09` vira
`12345678909`. O dígito verificador **não** é validado por algoritmo — apenas a
quantidade de dígitos e a unicidade, que o índice único em `usuarios.Cpf` garante.

> Vale registrar: a validação real do dígito verificador é pendência conhecida.

### Endereço

Quando `requisicao.Endereco` vem preenchido:

1. Remove os endereços do usuário marcados como `Padrao`.
2. Consulta o CEP no **ViaCEP** para obter logradouro, bairro, cidade e estado.
3. Cria o endereço com `Padrao = true`.
4. Geocodifica o endereço completo no **Nominatim** para obter latitude e longitude.
5. Grava latitude/longitude quando a geocodificação responde.

Passo 1 é o que motivou a mudança de modelo do pedido — ver
[`05-enderecos-e-pedidos.md`](05-enderecos-e-pedidos.md).

Se o ViaCEP não responder, os campos de texto vêm vazios e o endereço é salvo assim
mesmo; a geocodificação só é aplicada quando há resultado.

---

## `UsuarioDto`

```csharp
precisaPerfil = string.IsNullOrEmpty(usuario.Cpf);
temEndereco  = usuario.Enderecos?.Any(a => a.Padrao) ?? false;
```

O frontend usa `precisaPerfil` para redirecionar ao completar cadastro, e
`temEndereco` para decidir entre pedir endereço e pular para a loja. Ambos vêm
calculados no backend — o frontend não tem como inferir isso a partir dos demais
campos.
