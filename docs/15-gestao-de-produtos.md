# 15 — Gestão de Produtos e Categorias

**Status:** ✅ concluído

---

## Objetivo

Permitir cadastrar, editar, ilustrar, precificar e organizar produtos e categorias sem
tocar no banco. É a escrita da loja, atrás da policy `SomenteAdministrador`.

Detalhamento do armazenamento de imagens e da melhoria de texto por IA:
[`13-integracoes-pendentes.md`](13-integracoes-pendentes.md).

---

## Endpoints

### Produtos — `api/admin/produtos`

| Método | Rota | O que faz |
|---|---|---|
| `GET` | `/api/admin/produtos` | Todos, ativos e inativos |
| `GET` | `/api/admin/produtos/{id}` | Um produto pelo **id** |
| `POST` | `/api/admin/produtos` | Cria |
| `POST` | `/api/admin/produtos/{id}/atualizar` | Atualiza |
| `POST` | `/api/admin/produtos/{id}/excluir` | Exclui e apaga os arquivos do disco |
| `POST` | `/api/admin/produtos/texto/melhorar` | Reescreve a descrição por IA |
| `POST` | `/api/admin/produtos/{id}/imagens` | Envia uma imagem (`multipart/form-data`, campo `arquivo`) |
| `POST` | `/api/admin/produtos/{id}/imagens/{imagemId}/excluir` | Exclui imagem e reindexa a ordem |
| `POST` | `/api/admin/produtos/{id}/imagens/ordem` | Reordena pelo conjunto de ids |
| `POST` | `/api/admin/produtos/{id}/variantes` | Cria uma opção |
| `POST` | `/api/admin/produtos/{id}/variantes/{varianteId}/atualizar` | Atualiza uma opção |
| `POST` | `/api/admin/produtos/{id}/variantes/{varianteId}/excluir` | Exclui se ainda não estiver em pedido |
| `POST` | `/api/admin/produtos/{id}/variantes/ordem` | Reordena as opções |
| `POST` | `/api/admin/produtos/{id}/variantes/{varianteId}/estoque/somar` | Soma unidades ao estoque da opção |

### Categorias — `api/admin/categorias`

| Método | Rota | O que faz |
|---|---|---|
| `GET` | `/api/admin/categorias` | Todas, ativas e inativas |
| `POST` | `/api/admin/categorias` | Cria |
| `POST` | `/api/admin/categorias/{id}/atualizar` | Atualiza |
| `POST` | `/api/admin/categorias/{id}/excluir` | Exclui, se não houver produto vinculado |

Todas exigem `[Authorize(Policy = "SomenteAdministrador")]`.

Excluir responde `204` sem corpo. Os controllers traduzem as exceções do service em
`404` (`KeyNotFoundException`) e `400` (`InvalidOperationException`), com `{ message }` —
a mensagem é escrita para a pessoa que vai ler na tela, não para o log.

### Por que não há `PUT` nem `DELETE`

O servidor de produção só encaminha `GET`, `POST`, `HEAD`, `OPTIONS` e `TRACE`.
`PUT` e `DELETE` são recusados pelo IIS **antes de chegar na API**, com `405` e sem
nenhum header de CORS — o navegador reporta isso como "bloqueado pela política de
CORS", mensagem que aponta para o lado errado e esconde a causa.

Atualizar, excluir e reordenar são `POST` com o verbo no fim da URL.
`VerboHttpDasRotasTests` trava a regra e falha se um controller do painel declarar
`PUT`, `DELETE` ou `PATCH`. **Voltar para `DELETE` parece só uma melhoria de estilo
REST, e a quebra só aparece em produção, no painel, como erro de CORS.**

---

## `GestaoDeProdutosService`

### O que é validado

| Regra | Mensagem |
|---|---|
| Nome obrigatório | "O nome do produto é obrigatório." |
| Venda e custo não negativos | "Os preços não podem ser negativos." |
| Estoque não negativo | "A quantidade em estoque não pode ser negativa." |
| Categoria existe **e está ativa** | "Categoria inválida ou inativa." |
| Promocional não negativo | "O preço promocional não pode ser negativo." |
| Promocional **menor** que o de venda | "O preço promocional precisa ser menor que o preço de venda." |

A categoria inativa é recusada de propósito. Se ela passasse, o produto ficaria invisível
na vitrine e ninguém saberia por quê.

### Slug

**Não é campo obrigatório.** Vazio, a API deriva do nome e resolve colisão com sufixo
numérico (`batom-matte`, `batom-matte-2`).

```csharp
var normalizado = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
```

A normalização em `FormD` é o que separa o acento: "Batom Matte" vira `batom-matte` e
não `batom-matte-` com acento combinante sobrando. O `GerarSlug` é `internal static`
porque `GestaoDeCategoriasService` reaproveita — duplicar a normalização em dois lugares
garantiria que as duas divergissem na primeira exceção.

### Exclusão recusa produto vinculado a pedido

```csharp
catch (DbUpdateException)
{
    throw new InvalidOperationException(
        "Este produto está vinculado a pedidos e não pode ser excluído. Desative-o para ocultá-lo na vitrine.");
}
```

`Produto` → `ItensPedido` é `Restrict`, e é isso que protege o histórico. A mensagem
oferece a saída: desativar. O mesmo vale para categoria com produto vinculado, com
verificação explícita antes do `Remove`.

A exclusão **apaga os arquivos do disco depois** do `SaveChanges`, nunca antes: se o
banco recusar, o arquivo ainda está lá e o produto continua com a foto.

### Imagens

- Máximo de **3 por produto**, em `IGestaoDeProdutosService.MaximoDeImagensPorProduto`.
- A `Ordem` do novo arquivo é a quantidade atual — a última entra como última.
- **Reordenar valida o conjunto inteiro**, não só os ids:

```csharp
if (recebidos.Count != imagens.Count || !existentes.SetEquals(recebidos))
{
    throw new InvalidOperationException("A ordem informada não corresponde às imagens do produto.");
}
```

Aceitar um conjunto parcial deixaria o frontend exibir uma ordem que o servidor não
gravou. E o `SetEquals` impede que um id de outro produto entre na lista.

- **Excluir reindexa.** Depois de remover a imagem 1 de 3, as duas restantes ficam em 0 e
  1, e não em 0 e 2. Sem o reindex, a próxima imagem nova entraria com ordem 2 e a
  reordenação passaria a ter buraco.
- Se o `SaveChanges` falhar depois de gravar o arquivo, o arquivo é removido, para não
  deixar órfão no disco.

A gravação em si, com validação por assinatura de arquivo e nome gerado pelo servidor,
está em [`13-integracoes-pendentes.md`](13-integracoes-pendentes.md).

### `precoCusto` existe no DTO de administração e não no público

`ProdutoAdministracaoDto` carrega `PrecoCusto`; `ProdutoDto` não. Custo é informação
interna, e expor na API pública entregaria a margem de quem compra.

---

## Variantes de produto

Uma variante representa uma opção do produto, como uma cor ou um tipo. Cada uma tem
nome, estoque, preço adicional opcional, estado ativo e ordem. `CorHex` também é
opcional; a opção continua identificável pelo nome sem uma amostra de cor.

Os DTOs administrativos incluem opções ativas e inativas. O catálogo público inclui
somente opções ativas, ordenadas por `Ordem` e `Id`, com estoque e preço adicional. A
API valida que a opção pertence ao produto, está ativa e tem estoque suficiente na
criação de pedido. Se um produto tem opções ativas, a seleção é obrigatória.

O preço é calculado no servidor: valor promocional quando existe, senão valor de venda,
mais o adicional da opção. O preço que o frontend envia não é fonte de cobrança.
Quantidade solicitada é validada e baixada no estoque da variante; cancelamento devolve
as unidades para a mesma opção.

Uma opção usada por um pedido não pode ser excluída. Deve ser desativada para deixar de
ser oferecida, mantendo intacto o histórico. O pedido registra também o nome da opção
no momento da compra, para não alterar a descrição de pedidos antigos quando a opção
for renomeada.

As migrations relacionadas são `EnsureVariantesInnoDB` e
`VariantesSemCorEHistoricoDePedido`. A segunda permite `CorHex` nulo e adiciona
`VarianteNomeRegistrado` nullable a `itens_pedido`; são mudanças aditivas. A migration
de correção assegura InnoDB nas tabelas relacionadas antes de criar a chave estrangeira.
**A documentação não implica que migrations pendentes já tenham sido aplicadas.**

---

## Preço promocional e destaque

Migration `20260928044006_PrecoPromocionalEDestaque`:

```csharp
Destaque           tinyint(1)  não nulo, padrão false, com índice
PrecoPromocional   decimal(10,2)  nulo
```

Os dois campos foram na **mesma migration** porque são a mesma ideia: curadoria do
produto, além de preço e estoque.

**`PrecoPromocional` é nulo, e não zero.** Nulo significa fora de promoção; zero seria uma
promoção de graça. A validação mora no `GestaoDeProdutosService` e recusa um promocional
igual ou maior que o de venda — sem ela dava para "promover" um produto de R$ 39,90 para
R$ 50, e a vitrine mostraria R$ 39,90 riscado com R$ 50 ao lado.

O índice em `Destaque` existe porque a home filtra por ele a cada visita.

### `GET /api/produtos/destaques`

Entre os produtos **ativos** e marcados. Um destaque inativo não aparece: desativar
precisa tirar da vitrine, senão a home mostraria algo que o cliente não consegue
comprar. Esse caso tem teste.

A seleção de colunas do `CatalogoService` foi para um método privado `Projetar`, e não
repetida em três consultas. Copiar três vezes a mesma lista de campos é como a próxima
mudança no DTO deixa de lembrar de uma delas.

---

## `GestaoDeCategoriasService`

Mesma forma do service de produtos: valida nome, gera slug único e recusa exclusão com
produto vinculado.

A descrição vazia vira `null`, e não `""`. No catálogo, `null` é o que faz a vitrine cair
no texto padrão em vez de mostrar um parágrafo em branco.

---

## Testes

`GestaoDeProdutosServiceTests` (32 casos) e `GestaoDeCategoriasServiceTests` são as
suítes novas. A suíte do projeto foi de 169 para **296 testes** no momento desta entrega, todos passando.

| Grupo | Casos |
|---|---|
| Leitura | Traz ativos e inativos com imagens ordenadas; `null` para id inexistente |
| Slug | A partir do nome; respeita o informado; desambigua com sufixo numérico |
| Validação | Nome obrigatório, preços negativos, estoque negativo, categoria inexistente, categoria **inativa** |
| Promoção | Guarda o promocional; sem promoção deixa `null`; recusa acima, igual e negativo; edição altera e pode limpar |
| Destaque | Guarda a flag |
| Exclusão | Remove produto e apaga os arquivos; lança para id inexistente |
| Imagens | Grava com a ordem seguinte; bloqueia a quarta; lança para produto inexistente; propaga erro do armazenamento |
| Reordenação | Grava a nova ordem; recusa conjunto divergente; recusa ids duplicados; lança quando o produto não tem imagens |
| Excluir imagem | Remove o registro, reindexa e apaga o arquivo; lança quando a imagem é de outro produto |

O teste do slug por acento não existe com nome próprio: ele é coberto indiretamente por
`CriarAsync_gera_slug_a_partir_do_nome`, e vale saber disso, porque o `Normalize(FormD)`
é exatamente o que quebra sem aviso quando alguém "simplifica" a geração de slug.

### O que **não** tem teste, e por quê

| Caminho | Motivo |
|---|---|
| `ExcluirAsync` com produto vinculado a pedido | O provider InMemory **não aplica** `Restrict`, então o `DbUpdateException` nunca acontece no teste |
| Limpeza do arquivo quando o `SaveChanges` falha depois de gravar | Precisaria forçar falha de banco com o arquivo já no disco |
| Slug que gera base vazia (nome só com símbolos) | O `GerarSlug` é `internal` e o caminho é alcançável, mas ficou sem caso |
| Os `catch` de tradução em `AdminProdutosController` | Ver [`14-testes-e-qualidade.md`](14-testes-e-qualidade.md) |

O primeiro é o que mais importa: a mensagem "está vinculado a pedidos" é a que impede
o admin de perder um produto com histórico, e ela hoje não é exercitada por nenhum
teste.

---

## Pendente

- **Endpoint dedicado para o destaque.** A tela alterna a estrela reenviando o produto
  inteiro. Funciona porque a atualização substitui todos os campos, mas
  qualquer campo novo que a tela não conheça passa a poder ser apagado por essa troca.
- **Paginação administrativa** em `GET /api/admin/produtos`. O catálogo público
  (`GET /api/produtos/paginados`) já aceita paginação e filtro por categoria; ver
  [`12-catalogo.md`](12-catalogo.md).

---

## Entregue nesta etapa: controle de estoque (backend)

A tabela `movimentos_estoque` (migration `CriacaoDaTabelaDeMovimentosEstoque`) registra
entrada, saída e ajuste com rastreabilidade completa:

| Campo | Tipo | Papel |
|---|---|---|
| `ProdutoId` | `bigint` | FK para `produtos` (`Restrict`) |
| `Tipo` | `tinyint` | 0=Entrada, 1=Saída, 2=Ajuste |
| `Quantidade` | `int` | valor absoluto |
| `Referencia` | `varchar(100)` | ex.: "Pedido #123", "Soma manual" |
| `Observacao` | `varchar(500)` | detalhe livre |
| `UsuarioId` | `bigint` | quem fez (`SetNull`) |
| `CriadoEm` | `datetime(6)` | UTC |

Endpoints novos em `AdminProdutosController`:

| Método | Rota | O que faz |
|---|---|---|
| `POST` | `/api/admin/produtos/{id}/estoque/somar` | Soma (positivo) ou subtrai (negativo) unidades do estoque, criando movimento do tipo `Entrada` ou `Saida` |
| `GET` | `/api/admin/produtos/{id}/estoque/movimentos` | Lista movimentos do produto, mais recentes primeiro |
| `POST` | `/api/admin/produtos/{id}/estoque/movimentos` | Registra movimento manual (`Entrada`/`Saida`/`Ajuste`) com referência e observação |

`SomarQuantidadeEstoqueAsync` faz upsert atômico: valida que o resultado não fique
negativo, atualiza `Produto.QuantidadeEstoque` e insere o `MovimentoEstoque` na mesma
transação. O campo "somar ao estoque" no formulário do painel chama esse endpoint.

O endpoint `RegistrarMovimentoEstoqueAsync` permite registrar `Ajuste` (define o
estoque para o valor informado) e movimentos com referência/observação livres, para
casos como inventário, perda, doação, etc.

Ver [`00-visao-geral-e-roadmap.md`](00-visao-geral-e-roadmap.md) (seção "Entregue e fora
da lista de pendências").
