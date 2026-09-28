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
| Testes | **296**, todos passando |
| Cobertura de linhas | **97,4%** |
| Cobertura de branches | **88,7%** |
| Banco necessário | nenhum |
| Rede necessária | nenhuma |

> A cobertura de linhas **caiu de 100%** com o código de produtos. Não foi uma
> regressão de qualidade dos testes: os 127 testes novos vieram, e o que ficou por
> fora é o mapeamento de exceção dos controllers novos. Está detalhado em
> [Branches e linhas parciais restantes](#branches-e-linhas-parciais-restantes), e é a
> dívida conhecida desta etapa.

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
| `CatalogoService` | Filtro de ativos, ordenação de imagens, slug inexistente, destaques |
| `GestaoDeProdutosService` | Slug, validações, promoção, imagens, reordenação, exclusão |
| `GestaoDeCategoriasService` | Slug, descrição vazia, exclusão com produto vinculado |
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
- Slug duplicado, com símbolo e a partir de nome que não gera texto útil.
- Categoria **inativa** recusada como destino de produto.
- Preço promocional nulo, negativo, igual e maior que o de venda.
- Reordenação de imagens com conjunto divergente e com ids duplicados.
- Imagem cujo registro pertence a outro produto.
- Produto com flag de destaque e inativo **fora** dos destaques.
- Arquivo com extensão `.png` mas assinatura de JPEG — a extensão gravada é a do
  conteúdo.

---

## Branches e linhas parciais restantes

O número de 100% de linhas era verdade até a etapa de produtos e **não é mais**. O que
ficou descoberto, medido com o comando de cobertura desta doc:

| Local | Linhas | Motivo |
|---|---|---|
| `ProdutosController` | 75% | linhas 26–29: o `return Ok` do endpoint de destaques. Nenhum teste exercita controller |
| `AdminProdutosController` | 87,9% | linhas 102–116 (os `catch` de `MelhorarTexto` e upload) e 140 (o `CreatedAtAction` da criação) |
| `ArmazenamentoDeImagensLocal` | 91,4% | limpeza do arquivo em falha de gravação (92), cabeçalho menor que a assinatura (192–193), pasta e nome original vazios (236, 254), e os dois `catch` de `File.Delete` (277–285) |
| `DependencyInjection` | 93% | linhas 65–68: registro das integrações sem configuração |
| `DtosDeDominio` | 93,5% | linhas 20–21, 41 e 43: construtores que nenhuma prova usa |
| `GestaoDeProdutosService` | 95,5% | o `catch` de `DbUpdateException` na exclusão (113–116), a limpeza do arquivo quando o `SaveChanges` falha (158–161) e o slug com base vazia (300–302) |
| `Program` | 95,9% | blocos de seed e o registro de arquivos estáticos em desenvolvimento (118–127) |

Há duas linhas nessa lista que **não** deveriam estar descobertas, e são a mesma
história: `ComecaCom` com cabeçalho menor que a assinatura e `SanearNomeOriginal` com
nome vazio têm teste correspondente no arquivo
`ArmazenamentoDeImagensLocalTests.cs`. Ou o teste não chega na linha, ou a linha
reportada está deslocada. Fechar a meta de cobertura exige resolver isso primeiro — e é
exatamente o tipo de coisa que a meta de 100% servia para expor.

A parte que **não** é só origem de teste é a dos `catch` dos controllers: eles traduzem
exceção em status HTTP, e status errado é contrato de API. A suíte cobre a exceção no
service, mas não a tradução. Fechar isso exige teste de controller, e o projeto não tem
biblioteca de teste de integração — decisão que valia para 169 testes e começa a pesar
em 296.

Branches: 88,7% no total, e os parciais antigos continuam valendo:

| Local | Motivo |
|---|---|
| `AutenticacaoService` linha 222 | `usuario.Enderecos?` nulo — só em entidade sem `Include` |
| `CadastrarAsync` linha 41 | Nome vazio do Google, ou `TokenRecaptcha` nulo |
| `JwtTokenService` 93–94 | `JwtRegisteredClaimNames.Typ` / `Sub` ausentes num token forjado |
| `RecaptchaValidator` 60, 62 | Resposta sem score, e múltiplos códigos de erro |
| `MelhoradorDeTextoOpenAiCompativel` | formato de resposta inesperado do provedor de IA |
| `Program` 48, 63 | Blocos de seed e perfil de ambiente |

### O que foi marcado como não testável

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

---

## O que não é testado

| Item | Motivo |
|---|---|
| Migrations | Geradas por tooling; exigiria banco real |
| `Program.cs` (composition root) | Precisa de aplicação de pé |
| Construtores e getters vazios | Sem comportamento a verificar |
| Mapeamento de exceção dos controllers | Ver [acima](#branches-e-linhas-parciais-restantes) |
| Constraints do MySQL | O provider InMemory não as aplica |
| `Restrict` na exclusão de produto e de item de pedido | O InMemory não aplica restrição de FK; a exceção nunca acontece no teste |
| Arquivo bloqueado ou sem permissão ao excluir | Os dois `catch` de `File.Delete` só-discos não são alcançáveis com pasta temporária |

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

> E o par do teste: a exceção é testada no service, e a **tradução** dela em status HTTP
> precisa de teste no controller. Testar a exceção sem testar a tradução deixa a camada
> que o cliente realmente vê sem cobertura nenhuma — foi assim que a cobertura de linha
> caiu ao entrar o CRUD de produtos.

**Teste a regra, não o exemplo.** "Recusa quando a categoria está inativa" vale mais do
que "recusa quando a categoria 7 está inativa": a primeira continua valendo quando o id
muda.
