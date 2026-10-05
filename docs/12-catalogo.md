# 12 — Catálogo de Produtos e Categorias

**Status:** ✅ concluído (somente leitura)

---

## Objetivo

Expor o catálogo da loja: listar e paginar produtos ativos, buscar um produto pelo slug
e listar categorias ativas. Produtos públicos incluem imagens e opções ativas ordenadas.

---

## Endpoints

| Método | Rota | Resposta |
|---|---|---|
| `GET` | `/api/produtos` | Lista de produtos ativos |
| `GET` | `/api/produtos/paginados` | Página de produtos, opcionalmente filtrada por categoria |
| `GET` | `/api/produtos/destaques` | Lista de produtos ativos **e** marcados como destaque |
| `GET` | `/api/produtos/{slug}` | Produto ou `404` |
| `GET` | `/api/categorias` | Lista de categorias ativas |

Rotas públicas, sem `[Authorize]`. A vitrine precisa funcionar para quem ainda não
entrou na conta.

O literal `destaques` convive com o `{slug}` na mesma rota porque **segmento literal tem
precedência sobre segmento de parâmetro** no roteamento do ASP.NET Core. A ordem em que
os métodos são declarados não muda nada — declarar o `{slug}` primeiro também funcionaria
—, mas a diferença é invisível no código, e depender dela sem saber seria pedir para
quebrar na próxima reordenação.

---

## `CatalogoService`

Implementa `ICatalogoService`, da camada `Application`. Os controllers só conhecem a
interface:

```csharp
[HttpGet]
public async Task<IActionResult> ObterTodos(CancellationToken cancellationToken)
{
    var produtos = await _catalogoService.ObterProdutosAtivosAsync(cancellationToken);
    return Ok(produtos);
}
```

Não há lógica de catálogo nos controllers — eles apenas traduzem HTTP.

---

## Paginação e filtro por categoria

`GET /api/produtos/paginados` aceita:

| Query param | Padrão | Regra |
|---|---:|---|
| `pagina` | `1` | Inteiro positivo |
| `tamanhoPagina` | `12` | Inteiro entre 1 e 100 |
| `categoriaSlug` | ausente | Filtra produtos pela categoria ativa |

Exemplo:

```text
GET /api/produtos/paginados?pagina=2&tamanhoPagina=12&categoriaSlug=bases
```

O endpoint responde `400` se página ou tamanho forem inválidos. A resposta contém
`itens`, `pagina`, `tamanhoPagina`, `totalItens` e `totalPaginas`. O total é contado
antes do `Skip`/`Take`. Uma categoria inexistente ou inativa produz uma página vazia.

A ordenação é estável por `Nome` e depois `Id`; sem o segundo critério, nomes iguais
poderiam mudar de posição entre páginas. Filtro, contagem, ordenação, paginação e
projeção são executados na consulta ao banco. `/api/produtos` continua disponível para
compatibilidade e para consumidores que ainda pedem a lista completa; a vitrine usa a
rota paginada.

---

## Listagem

As rotas de listagem reutilizam `ProjetarProduto`, uma expressão comum que projeta
produto, categoria, imagens e variantes diretamente em `ProdutoDto`. Não materializam
entidades nem carregam custos administrativos.

```csharp
var itens = await consulta
    .OrderBy(p => p.Nome)
    .ThenBy(p => p.Id)
    .Skip((pagina - 1) * tamanhoPagina)
    .Take(tamanhoPagina)
    .Select(ProjetarProduto)
    .ToListAsync(cancellationToken);
```

Três decisões nessa consulta:

**`Where(p => p.Ativo)`** filtra no banco. `Produto.Ativo` e `Categoria.Ativo` existem
exatamente para isso: um produto descontinuado some da vitrine sem precisar ser
excluído, e o histórico de pedidos que o referenciam continua íntegro. Por isso
`Produto` → `ItensPedido` é `Restrict`.

**`Select(...)` projeta direto no DTO.** Sem `Include`, o EF monta o `ProdutoDto` sem
materializar o grafo de entidades.

**`OrderBy(i => i.Ordem)` dentro da projeção** ordena as imagens já no banco, então o
frontend recebe a principal primeiro sem precisar reordenar. Categorias e produtos são
ordenados por `Nome`.

`AsNoTracking()` aparece nas consultas de catálogo: nenhuma delas altera dados, e dispensar
o rastreamento evita materializar o grafo de concorrência sem necessidade.

---

## Busca por slug

```csharp
[HttpGet("{slug}")]
public async Task<IActionResult> ObterPorSlug(string slug, CancellationToken cancellationToken)
{
    var produto = await _catalogoService.ObterProdutoPorSlugAsync(slug, cancellationToken);
    return produto is null ? NotFound() : Ok(produto);
}
```

A rota usa **slug** (`batom-liquido-matte`) e não `id` — a URL fica legível e
compartilhável. `produtos.Slug` tem índice único, então a busca é direta.

O filtro também exige `p.Ativo`: um produto descontinuado responde `404`, igual a um
inexistente. O controller não distingue os dois casos, porque não há nada a distinguir
para quem consulta.

---

## DTOs

| DTO | Conteúdo |
|---|---|
| `ProdutoDto` | Id, nome, slug, descrição, preço de venda, preço promocional, estoque base, ativo, destaque, categoria, imagens e variantes ativas |
| `VarianteProdutoLojaDto` | Id, nome, cor opcional, estoque, preço adicional opcional, estado e ordem |
| `ProdutosPaginadosDto` | Itens e metadados da página |
| `CategoriaDto` | Id, nome, slug, descrição, ativo |
| `ImagemProdutoDto` | Id, caminho relativo, nome original, ordem |

Os preços e o estoque chegam **prontos no DTO**. O mapeamento acontece na projeção da
consulta, então o frontend nunca manipula entidade do domínio nem precisa saber como o
banco guarda o dado.

`NomeCategoria` vem junto de `ProdutoDto` para evitar uma segunda chamada para
montar o card do produto.

`precoVenda` continua no DTO mesmo existindo `precoPromocional`, porque é o valor cheio
e precisa aparecer riscado ao lado do promocional. Fora de promoção, quem manda é o de
venda.

> O preço de **custo** não vai ao DTO — é dado financeiro interno e não deve chegar ao
> navegador. Ele existe em `ProdutoAdministracaoDto`, que só sai pela rota de
> administração: ver [`15-gestao-de-produtos.md`](15-gestao-de-produtos.md).

---

## Uma projeção compartilhada

`ObterProdutosAtivosAsync`, `ObterProdutosPaginadosAsync`, `ObterProdutoPorSlugAsync` e
`ObterProdutosDestaqueAsync` usam a **mesma**
`Expression<Func<Produto, ProdutoDto>>` privada. Assim preço, imagens e variantes são
projetados de maneira consistente em todas as rotas públicas.

---

## Escrita e estoque

A API pública é **somente leitura**, e isso é definitivo: a escrita existe, mas atrás de
`api/admin/produtos` e `api/admin/categorias`, protegidas pela policy
`SomenteAdministrador` — ver [`15-gestao-de-produtos.md`](15-gestao-de-produtos.md).

O estoque é mantido nas rotas administrativas e na criação/cancelamento de pedidos. A
gestão das opções e os efeitos da variante sobre preço e estoque estão descritos em
[`15-gestao-de-produtos.md`](15-gestao-de-produtos.md).
