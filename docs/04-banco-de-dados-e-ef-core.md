# 04 — Banco de Dados e EF Core

**Status:** ✅ concluído

---

## Objetivo

Modelar o esquema no MySQL com Entity Framework Core, mantendo as entidades livres de
atributos de ORM e todas as decisões de mapeamento em um lugar só.

---

## `LumiDbContext`

```csharp
public class LumiDbContext : DbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Endereco> Enderecos => Set<Endereco>();
    // ... 12 DbSets no total
}
```

Os `DbSet` usam o nome da tabela em português, com o singular da entidade. Nenhum é
`virtual` — o provider é o MySQL via Pomelo, então não há lazy loading e o acesso é
sempre explícito com `Include`.

`OnModelCreating` aplica todas as configurações por convenção:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(LumiDbContext).Assembly);
```

Uma linha e pronto — não há lista para manter. Uma `IEntityTypeConfiguration<T>` nova
no assembly é aplicada automaticamente.

---

## Strategy

Mapeamento por classe, com `ToTable` explícito:

```csharp
builder.ToTable("pedidos");
builder.HasKey(o => o.Id);
```

Nome de tabela **snake_case plural**, sempre declarado. Nomes explícitos tornam a
migration legível e impedem que uma mudança de convenção do Pomelo quebre o schema
sem alguém perceber.

---

## Índices

| Tabela | Índice | Único |
|---|---|---|
| `usuarios` | `Email` | sim |
| `usuarios` | `IdGoogle` | sim |
| `usuarios` | `Cpf` | sim |
| `produtos` | `Slug` | sim |
| `categorias` | `Slug` | sim |
| `recuperacoes_de_senha` | `HashToken` | sim |

`Email`, `IdGoogle` e `Cpf` são únicos porque identificam a mesma pessoa — a regra é
garantida no banco, não apenas na aplicação. `HashToken` é único para tornar a busca
do token de redefinição um índice direto, e evitar colisão.

## Tabela `banners`

Criada na migration `CriacaoDaTabelaDeBanners`, aplicada **localmente** — ver
[16 — CI/CD e deploy](16-ci-cd-e-deploy.md) para por que ela nunca roda no deploy.

| Coluna | Tipo | Observação |
|---|---|---|
| `Id` | `bigint` | identidade |
| `CaminhoRelativoDesktop` | `varchar(500)` | arte de telas largas |
| `CaminhoRelativoMobile` | `varchar(500)` | arte de telas estreitas |
| `NomeOriginalDesktop` | `varchar(255)` | só o painel usa |
| `NomeOriginalMobile` | `varchar(255)` | só o painel usa |
| `TextoAlternativo` | `varchar(160)` | nulo quando não informado |
| `Ordem` | `int` | posição no carrossel, 0-based |
| `Ativo` | `tinyint(1)` | tira o slide do ar sem apagar |
| `CriadoEm` | `datetime(6)` | UTC |

Índices em `Ordem` e em `Ativo`, os dois da consulta da vitrine
(`WHERE Ativo ORDER BY Ordem`). Nenhum é único: `Ordem` é gravada como posição a
cada reordenação, e dois banners podem legitidamente estar inativos.

`Ordem` guarda a **posição**, não um número escolhido pelo cliente. É o que impede
"ordem 47" órfã quando um banner é excluído no meio da lista.

Ver [`17-banners.md`](17-banners.md).

## Tabela `configuracao_whatsapp`

Uma linha só, gravada pelo painel. Existe para que a frase que abre a mensagem do
cliente possa ser editada sem republicar a API.

| Coluna | Tipo | Observação |
|---|---|---|
| `Id` | `bigint` | identidade |
| `MensagemInicialCliente` | `varchar(500)` | **obrigatória**, 500 é o limite do service |

Ver [`18-whatsapp-e-baileys.md`](18-whatsapp-e-baileys.md).

> **O tamanho vem do serviço, e não de um atributo de modelo.** `HasMaxLength(500)`
> existe para o EF gerar o schema; quem recusa a frase com mensagem legível é
> `GestaoDeWhatsAppService`. A regra é de negócio, e o texto que a administradora lê
> é mais útil que um erro genérico de validação do ASP.NET.

## Nomes de coluna são PascalCase no banco

`DistanciaKm` é mapeada como `DistanceKm`, não `distancia_km` nem `distanciaKm`.

A regra vale para as colunas criadas pelo `RenomearParaPortugues`, e é o que o MySQL
está com. A consequência prática apareceu quando o mapeamento passou a declarar
`HasColumnName("distanceKm")` para a coluna de distância: o EF passa a descrever uma
coluna que não existe com esse nome, e `dotnet ef migrations add` gerava uma alteração
de coluna que o banco não precisava — a comparação de nomes no MySQL é
case-insensitive, então nada acusava o erro.

> **Isso não é a mesma coisa que `HasColumnName` ser ignorado.** O valor gravado é
> idêntico nos dois casos; a diferença é que o snapshot passa a descrever algo diferente
> do que existe, e o schema gerado pela migration seguinte não corresponde ao banco. O
> nome declarado no mapeamento e o nome real da coluna foram conferidos direto em
> `INFORMATION_SCHEMA`, e `has-pending-model-changes` passou a responder *no changes*.

> O índice único em `Cpf` convive com `Cpf` anulável: múltiplos `NULL` são aceitos
> pelo MySQL, o que permite que usuários sem CPF completo existam até a conclusão
> do perfil.

---

## Comportamento de exclusão

| Relação | Comportamento |
|---|---|
| `Usuario` → `Enderecos` | `Cascade` |
| `Usuario` → `Pedidos` | `Restrict` |
| `Usuario` → `Despesas` | `Restrict` |
| `Pedido` → `Itens` | `Cascade` |
| `Pedido` → `NotasFiscais` | `Cascade` |
| `Pedido` → `RegistrosWhatsApp` | `Cascade` |
| `Produto` → `ItensPedido` | `Restrict` |
| `ImagemProduto` → `Produto` | `Cascade` |
| `Usuario` → `RecuperacoesDeSenha` | `Cascade` |

A regra: quando o filho é **parte** do pai, `Cascade`; quando é **histórico que
precisa sobreviver**, `Restrict`.

Assim, apagar um pedido leva junto seus itens — não faz sentido manter item de um
pedido inexistente. Mas apagar um usuário é bloqueado se ele tiver pedidos: o
histórico de vendas não pode sumir junto da conta. O mesmo vale para produto com
itens pedidos e para despesa de usuário.

> Essa é uma consequência direta da mudança de endereço descrita em
> [`05-enderecos-e-pedidos.md`](05-enderecos-e-pedidos.md): como o pedido não aponta
> mais para `enderecos`, apagar um endereço da agenda do usuário não afeta pedidos
> antigos.

---

## Migrations

### Como aplicar

A API **não** migra o banco na inicialização — não existe chamada a `Migrate()` nem
`EnsureCreated()` no `Program.cs`. Também **não** há migration no deploy, e não haverá:
o workflow não tem nenhum passo de banco, e o servidor não é lugar para aplicar schema.

A aplicação acontece **na máquina de desenvolvimento**, contra o mesmo banco que serve a
produção:

```bash
dotnet ef database update --project src\LumiMakeup.Infrastructure --startup-project src\LumiMakeup.Api
```

> **Por que assim.** O banco é único: o de desenvolvimento é o de produção. Não existe
> cópia para testar migration, então aplicar é um ato local e consciente, com o banco em
> uso por outros. O que muda o schema é o código — a migration versionada, revisada em pull
> request — e a aplicação é consequência de puxar esse código, não um passo de deploy.
> Se someday vier um segundo banco, essa decisão precisa ser revista: aí passa a ter um
> script de migration separado, com backup, e ainda fora do deploy.

### Como criar

```bash
dotnet ef migrations add NomeDaMudanca --project src\LumiMakeup.Infrastructure --startup-project src\LumiMakeup.Api --output-dir Persistence\Migrations
```

Cada migration gera três arquivos: o `.cs` com `Up`/`Down`, o `.Designer.cs` com o
snapshot do modelo naquele ponto, e a atualização do
`LumiDbContextModelSnapshot.cs`.

### Histórico

| Migration | Conteúdo |
|---|---|
| `InitialCreate` | Esquema inicial completo |
| `RenomearParaPortugues` | Tabelas, colunas e enums convertidos para português |
| `AdicionarRecuperacaoDeSenha` | Tabela `recuperacoes_de_senha` |
| `RestaurarIntegridadeReferencial` | MyISAM → InnoDB e criação das 10 FKs |
| `EnderecoDeEntregaNoPedido` | Endereço próprio no pedido (ver doc `05`) |
| `ImagemProdutoComCaminhoRelativo` | Caminho relativo no banco em vez do absoluto do servidor |
| `PrecoPromocionalEDestaque` | Colunas de promoção e destaque em `produtos` |
| `CriacaoDaTabelaDeBanners` | Tabela `banners` (ver doc `17`) |
| `NucleoDePedido` | Tabelas de pedido, item e pagamento |
| `CupomDeDesconto` | Tabela `cupons_desconto` |
| `NotaFiscalGeradaNoPedido` | Vínculo de NF-e com o pedido |
| `EmailDeContatoNoPedido` | E-mail de contato no pedido |
| `SessaoDoWhatsApp` | Tabela da sessão pareada |
| `MensagemInicialDoWhatsApp` | Tabela `configuracao_whatsapp` (ver doc `18`) |

As 14 estão aplicadas no banco que serve a produção. A última, a
`20261002180111_MensagemInicialDoWhatsApp`, só cria tabela nova — não toca em dado
existente.

> A API precisa estar **parada** para rodar `dotnet ef`: o build tenta copiar as DLLs
> para `bin/` e falha se o processo estiver segurando o arquivo.

### O banco era MyISAM, e MyISAM não tem chaves estrangeiras

O servidor de desenvolvimento está com `default_storage_engine = MyISAM`, e **todas** as
13 tabelas foram criadas nele. MyISAM ignora a clause `CONSTRAINT` ao criar a tabela, sem
avisar. Por isso nenhuma das 10 FKs que o modelo do EF declara existiu no banco, e ninguém
percebeu: a `InitialCreate` as pediu, o MariaDB aceitou o `CREATE TABLE` e simplesmente
descartou.

A consequência era maior do que a integridade referencial. MyISAM também não tem
transações, então:

- O `SaveChanges` do EF **não era atômico**. Uma falha no meio de um pedido deixava
  itens gravados sem o pedido.
- O `START TRANSACTION` / `COMMIT` que o EF emite ao aplicar uma migration era um no-op.
- Um lock de escrita travava a tabela inteira, não só a linha.

A migration `RestaurarIntegridadeReferencial` converte as 13 tabelas para InnoDB e só
então cria as 10 FKs, com os nomes e o `ON DELETE` que o modelo declara. Verificado no
banco: um `INSERT` com `UsuarioId` inexistente agora é rejeitado com o erro 1452, e um
`INSERT` seguido de `ROLLBACK` não deixa rastro.

> O `default_storage_engine` do servidor continua em MyISAM. Qualquer tabela nova criada
> fora de migration nasce em MyISAM e perde as garantias de novo. Para um servidor
> dedicado, o certo é `default_storage_engine = InnoDB` na configuração do MariaDB.

### `DROP FOREIGN KEY IF EXISTS` é sintaxe do MariaDB

A `EnderecoDeEntregaNoPedido` usa `DROP FOREIGN KEY IF EXISTS`, que existe no MariaDB
10.11 e **não** existe no MySQL. Ela precisa disso porque, na altura, a FK de
`pedidos.EnderecoEntregaId` não existia no banco: um `DropForeignKey` incondicional, como
o EF gerou, aborta com o erro 3940. E o índice não podia ser removido antes da FK, porque
o MariaDB recusa com *"Cannot drop index: needed in a foreign key constraint"* — algo que
só ficou claro testando os quatro caminhos possíveis numa tabela de teste.

O provedor continua sendo o Pomelo, e o código C# não muda nada por causa disso — só o SQL
escrito à mão dentro das migrations.

---

## Tipos de coluna

| Propriedade | Tipo | Motivo |
|---|---|---|
| `DistanciaKm` | `decimal(8,2)` | Precisa de 2 casas, não `float` |
| `CustoFrete`, `Subtotal`, `Total` | `decimal(10,2)` | Dinheiro nunca em ponto flutuante |
| `Observacoes` | `text` | Texto livre |
| `CriadoEm`, `PagoEm`, `EntregueEm` | `datetime` | Datas UTC |
| Strings | `varchar(n)` com `HasMaxLength` | Limite explícito por propriedade |

Valores monetários usam `decimal`. `float`/`double` introduz erro de arredondamento
em dinheiro — `0.1 + 0.2` não dá `0.3`.

---

## Instância de teste

Os testes usam `Microsoft.EntityFrameworkCore.InMemory`, que respeita as
configurações de modelo (tamanho máximo, conversões de enum) sem exigir MySQL:

```csharp
var contexto = new LumiDbContext(new DbContextOptionsBuilder<LumiDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options);
```

Isso valida o mapeamento sem custo de container. As constraints reais do MySQL —
índices únicos, `Restrict` — não são exercitadas por esse provider; para elas é
preciso um banco de verdade.

O InMemory também **não** acusa divergência de nome de coluna, porque guarda o modelo
como foi declarado e não como está no banco. Para isso o provider real é o único que
serve, e o comando é:

```bash
dotnet ef migrations has-pending-model-changes --project src\LumiMakeup.Infrastructure --startup-project src\LumiMakeup.Api
```

> `has-pending-model-changes` só existe no EF 8. Respondendo *no changes* significa que
> o snapshot descreve o modelo e o modelo descreve o banco. Ele não valida o schema já
> aplicado em produção: para isso a conferência tem que ser contra o banco, em
> `INFORMATION_SCHEMA`.
