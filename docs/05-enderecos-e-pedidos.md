# 05 — Endereço de Entrega no Pedido

**Status:** ✅ concluído

---

## Objetivo

Garantir que o endereço de entrega de um pedido **nunca mais mude**, mesmo que o
cliente edite, exclua ou troque os endereços da própria conta.

---

## O problema

O modelo anterior ligava o pedido ao endereço do usuário por chave estrangeira:

```csharp
public long EnderecoEntregaId { get; set; }
public Endereco? EnderecoEntrega { get; set; }
```

Isso parecia correto, mas quebrava a histórico do pedido sempre que a conta do
cliente mudasse. Três cenários reais destruíam o endereço de entregas passadas:

1. **`CompletarPerfilAsync` substitui o endereço padrão.** O método remove os
   endereços marcados como `Padrao` antes de adicionar o novo:

   ```csharp
   foreach (var enderecoPadraoAntigo in usuario.Enderecos.Where(a => a.Padrao).ToList())
   {
       usuario.Enderecos.Remove(enderecoPadraoAntigo);
   }
   ```

2. **Edição de endereço na agenda.** Alterar o logradouro reescrevia o registro de
   todos os pedidos que apontavam para ele.

3. **Exclusão de endereço.** O pedido ficaria com referência inválida — ou o
   `Restrict` impediria a exclusão, prendendo o usuário em um endereço antigo.

O problema conceitual é que a FK guardava uma **referência**, e o pedido precisa
guardar um **fato**: "este pedido foi enviado para este endereço, neste dia".

---

## A solução

O `Pedido` passa a ter o endereço **como cópia própria**, em campos seus:

```csharp
public long Id { get; set; }
public long UsuarioId { get; set; }
public string EnderecoCep { get; set; } = string.Empty;
public string EnderecoLogradouro { get; set; } = string.Empty;
public string EnderecoNumero { get; set; } = string.Empty;
public string? EnderecoComplemento { get; set; }
public string EnderecoBairro { get; set; } = string.Empty;
public string EnderecoCidade { get; set; } = string.Empty;
public string EnderecoEstado { get; set; } = string.Empty;
```

A propriedade `EnderecoEntrega` e a coluna `EnderecoEntregaId` foram **removidas**, e
com elas o relacionamento em `PedidoConfiguration`. Não existe mais FK de `pedidos`
para `enderecos`.

### Duas estruturas, dois papéis

| Estrutura | Papel | Mutabilidade |
|---|---|---|
| `Endereco` | Agenda do cliente — quem ele é e onde mora | Editável, pelo cliente |
| Campos `Endereco*` em `Pedido` | Destino de **uma** entrega específica | Imutável após a criação |

`Usuario.Enderecos` continua existindo, e `Padrao` continua marcando o principal.
O que mudou é que o pedido deixou de depender dessa agenda.

O padrão já é usado no projeto: `ItemPedido` grava `NomeProdutoRegistrado` e
`PrecoVendaUnitario` no momento da compra, justamente para que renomear o produto ou
mudar o preço não reescreva o passado. O endereço segue a mesma regra.

---

## Comportamento esperado

### Endereço de entrega é obrigatório

Todo pedido precisa de um endereço completo. Não existe pedido sem destino.

### No checkout, o cliente escolhe

- **Um endereço salvo** da agenda → os campos são copiados para o pedido.
- **Um endereço novo** → é gravado na agenda do cliente (para reutilizar depois) e
  então copiado para o pedido.

Assim a lista de endereços do cliente cresce com os endereços que ele já usou, e o
pedido fica independente.

### Frete é calculado no checkout

O frete depende do endereço de entrega, então só pode ser calculado quando o endereço
está escolhido — ou seja, no checkout. No carrinho, o valor aparece como
**"frete a calcular"**, sem número.

Isso vale para `ConfiguracaoFrete` (origem, preço por km, taxa mínima) e para a
distância, que depende das coordenadas do destino.

---

## Mapeamento

```csharp
builder.Property(o => o.EnderecoCep).HasMaxLength(9).IsRequired();
builder.Property(o => o.EnderecoLogradouro).HasMaxLength(200).IsRequired();
builder.Property(o => o.EnderecoNumero).HasMaxLength(20).IsRequired();
builder.Property(o => o.EnderecoComplemento).HasMaxLength(100);
builder.Property(o => o.EnderecoBairro).HasMaxLength(100).IsRequired();
builder.Property(o => o.EnderecoCidade).HasMaxLength(100).IsRequired();
builder.Property(o => o.EnderecoEstado).HasMaxLength(2).IsRequired();
```

Os limites seguem exatamente os de `EnderecoConfiguration`, para que o mesmo endereço
tenha a mesma representação nas duas tabelas. `Complemento` é o único opcional —
apartamento, bloco, sala ou uma referência de entrega.

---

## Migration

`EnderecoDeEntregaNoPedido` remove a FK e o índice, descarta a coluna
`EnderecoEntregaId` e adiciona as sete colunas `Endereco*` (seis obrigatórias, uma
opcional). O `Down` reconstrói a coluna e o relacionamento — útil para rollback em
ambiente de teste.

> **Atenção em dados existentes.** Como as colunas entram como `NOT NULL DEFAULT ''`,
> pedidos que já existiam no banco ficam com o endereço **vazio**. Se houver pedido
> anterior à migration, é preciso preencher os campos a partir do endereço que estava
> vinculado antes de aplicar. A migration de dados para produção ainda não foi
> escrita.

---

## O que falta implementar

A estrutura está pronta; o fluxo ainda não existe. Falta:

- Serviço de pedidos que copie o endereço escolhido para os campos do `Pedido`.
- Endpoints de criação e consulta de pedidos.
- Cálculo de frete no checkout usando `ConfiguracaoFrete`.
- CRUD da agenda de endereços do usuário.
- Migration de dados para os pedidos já gravados.

---

## Arquivos envolvidos

| Arquivo | Mudança |
|---|---|
| `Domain/Entities/Pedido.cs` | Campos próprios no lugar da FK |
| `Infrastructure/Persistence/Configurations/PedidoConfiguration.cs` | Limites de tamanho; relacionamento removido |
| `Infrastructure/Persistence/Migrations/*_EnderecoDeEntregaNoPedido.cs` | Nova migration |
| `tests/LumiMakeup.Tests/Domain/EntidadesTests.cs` | Asserções do novo endereço |
