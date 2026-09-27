# 14 — Testes e Qualidade

**Status:** ✅ concluído

---

## Objetivo

Manter a API coberta por testes automatizados, com suíte rápida e determinística,
capaz de rodar sem banco de dados e sem rede.

---

## Números atuais

| Métrica | Valor |
|---|---|
| Testes | **169**, todos passando |
| Cobertura de linhas | **100%** |
| Cobertura de branches | 93,5% (8 branches parciais) |
| Banco necessário | nenhum |
| Rede necessária | nenhuma |

---

## Como rodar

```bash
dotnet test tests\LumiMakeup.Tests\LumiMakeup.Tests.csproj
```

Com cobertura:

```bash
dotnet test tests\LumiMakeup.Tests\LumiMakeup.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=coverage.xml /p:ExcludeByFile="**/Migrations/**"
```

> A API precisa estar **parada** ao rodar os testes: o build copia as DLLs para `bin/`
> e falha se o processo estiver segurando o arquivo. Se estiver aberto no Visual
> Studio, use o botão *Parar*.

Relatórios de cobertura são ignorados pelo git (`coverage.*.xml`, `coverage.json`).

---

## Stack de teste

| Biblioteca | Papel |
|---|---|
| xUnit | Framework de teste |
| Moq | Mocks de interface |
| EF Core InMemory | Contexto de banco em memória |
| coverlet.msbuild | Cobertura |

Não há teste de snapshot, nem FluentAssertions, nem biblioteca de teste de integração:
o projeto usa apenas o essencial.

---

## Banco em memória

```csharp
var contexto = new LumiDbContext(new DbContextOptionsBuilder<LumiDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options);
```

Cada teste cria um banco novo com `Guid` no nome. Isso isola completamente os testes:
um dado criado em um teste não aparece em outro, sem precisar de `EnsureDeleted`.

O InMemory respeita as configurações do modelo — tamanho máximo de string, conversão
de enum para string, nulabilidade. Então os testes **validam o mapeamento do EF**
de verdade.

O que ele **não** exercita: índices únicos, `Restrict`, tipos de coluna do MySQL.
Para essas regras seria preciso um banco real.

---

## O que é testado

| Área | Foco |
|---|---|
| Entidades | Valores padrão, relacionamentos, coleções |
| `AutenticacaoService` | Cadastro, login, Google, vínculo de conta, perfil, CPF |
| `RecuperacaoDeSenhaService` | Geração e hash de token, expiração, uso único, template |
| `JwtTokenService` | Claims, expiração, recusa de token do tipo errado |
| `RecaptchaValidator` | Score, token ausente, resposta de erro |
| `ViaCepService` | CEP normalizado, CEP inválido, falha de rede |
| `NominatimService` | Geocodificação |
| `CatalogoService` | Filtro de ativos, ordenação de imagens, slug inexistente |
| `LumiDbContext` | Modelo válido, configurações aplicadas |
| `DependencyInjection` | Registro de serviços, connection string ausente |

### Cenários de borda que têm teste

- E-mail com caixa e espaços diferentes normalizando para o mesmo registro.
- Login em conta sem senha (criada via Google).
- Token de recuperação expirado, já usado e com hash correspondente a nenhum registro.
- Novo pedido de reset invalidando o anterior.
- Score de reCAPTCHA abaixo e acima do limite.
- Chave secreta de reCAPTCHA ausente — degrada para "aceitar".
- SMTP não configurado — registro do `EmailSenderStub`.

---

## Cobertura de linhas em 100%

Atingir 100% exigiu marcar explicitamente o que não tem como ser testado:

```csharp
[ExcludeFromCodeCoverage]
public async Task EnviarAsync(MailMessage mensagem, CancellationToken cancellationToken = default)
```

`EnviarAsync` do `EnviadorDeEmailSmtpViaClienteSmtp` abre conexão SMTP de verdade. Um
teste dele exigiria um servidor de e-mail e só verificaria que o protocolo funciona —
sem checar nada da nossa lógica.

`ConstruirMime`, o helper privado chamado por ele, recebeu a **mesma** marcação. Sem
isso ele aparecia como 11 linhas descobertas: inalcançável a partir de um método já
excluído, impossível de cobrir por teste unitário. A exclusão precisa acompanhar o
caminho de chamada inteiro, não só a entrada.

### Branches parciais restantes

8 branches não ficam em 100%, quase todos em caminhos de falha:

| Local | Motivo |
|---|---|
| `AutenticacaoService` linha 222 | `usuario.Enderecos?` nulo — só em entidade sem `Include` |
| `CadastrarAsync` linha 41 | Nome vazio do Google, ou `TokenRecaptcha` nulo |
| `JwtTokenService` 93–94 | `JwtRegisteredClaimNames.Typ` / `Sub` ausentes num token forjado |
| `RecaptchaValidator` 60, 62 | Resposta sem score, e múltiplos códigos de erro |
| `Program` 48, 63 | Blocos de seed e perfil de ambiente |

Fechar esses 8 exigiria testes que dependem de estado anômalo de biblioteca externa ou
de configuração — custo alto, valor baixo. A cobertura de linhas em 100% é a meta
prática; branches ficam como indicador, não como obrigação.

---

## O que não é testado

| Item | Motivo |
|---|---|
| Migrations | Geradas por tooling; exigiria banco real |
| `Program.cs` (composition root) | Precisa de aplicação de pé |
| Construtores e getters vazios | Sem comportamento a verificar |
| Controllers | Cobertos indiretamente pelo service, exceto o mapeamento de exceção |
| Constraints do MySQL | O provider InMemory não as aplica |

O teste de `EntidadesTests` é a exceção que prova a regra: ele instancia
`new Pedido()` e confere cada propriedade. Getters de entidade não têm lógica, mas o
teste garante que um campo **novo** não seja esquecido na inicialização do objeto.

---

## O que observar ao escrever teste

**Teste comportamento, não implementação.** Se o teste passa quando um método privado
muda, ele está testando coisa errada.

**Nome descritivo em português**, no formato `Sujeito_verbo_complemento`:

```csharp
[Fact] public void Cadastrar_normaliza_o_email_e_define_o_papel_cliente()
```

**Banco novo por teste.** `Guid.NewGuid()` no nome evita vazamento de estado.

**Sem `Task.Delay`.** Teste assíncrono que espera tempo é teste instável. Para
`HttpClient`, use `HttpMessageHandler` falso em vez de um servidor local.

**Toda exception tem caminho de teste.** `InvalidOperationException` e
`UnauthorizedAccessException` são contrato de API — cada mensagem que o controller
traduz em 4xx precisa de teste.
