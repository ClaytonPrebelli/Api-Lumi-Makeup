# 12 — Catálogo de Produtos e Categorias

**Status:** ✅ concluído (somente leitura)

---

## Objetivo

Expor o catálogo da loja: listar produtos ativos, buscar um produto pelo slug e listar
categorias ativas, com as imagens ordenadas.

---

## Endpoints

| Método | Rota | Resposta |
|---|---|---|
| `GET` | `/api/produtos` | Lista de produtos ativos |
| `GET` | `/api/produtos/{slug}` | Produto ou `404` |
| `GET` | `/api/categorias` | Lista de categorias ativas |

Rotas públicas, sem `[Authorize]`. A vitrine precisa funcionar para quem ainda não
entrou na conta.

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

## Listagem

O filtro de atividade é aplicado **no banco**, não depois:

```csharp
return await _contexto.Produtos
    .AsNoTracking()
    .Where(p => p.Ativo)
    .OrderBy(p => p.Nome)
    .Select(p => new ProdutoDto(
        p.Id, p.Nome, p.Slug, p.Descricao,
        p.PrecoVenda, p.QuantidadeEstoque, p.Ativo,
        p.CategoriaId, p.Categoria.Nome,
        p.Imagens
            .OrderBy(i => i.Ordem)
            .Select(i => new ImagemProdutoDto(i.Id, i.UrlImagem, i.Ordem))
            .ToList()))
    .ToListAsync(cancellationToken);
```

Três decisões nessa consulta:

**`Where(p => p.Ativo)`** filtra no banco. `Produto.Ativo` e `Categoria.Ativo` existem
exatamente para isso: um produto descontinuado some da vitrine sem precisar ser
excluído, e o histórico de pedidos que o referenciam continua íntegro. Por isso
`Produto` → `ItensPedido` é `Restrict`.

**`Select(...)` projeta direto no DTO.** Sem `Include`, o EF monta o `ProdutoDto` com
uma consulta só e nunca materializa a entidade — nem categoria, nem imagens. É a
diferença entre um endpoint rápido e o equivalente carregando o grafo inteiro e
convertendo depois.

**`OrderBy(i => i.Ordem)` dentro da projeção** ordena as imagens já no banco, então o
frontend recebe a principal primeiro sem precisar reordenar. Categorias e produtos são
ordenados por `Nome`.

`AsNoTracking()` aparece nas três consultas: nenhuma delas altera dados, e dispensar
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
| `ProdutoDto` | Id, nome, slug, descrição, preço de venda, estoque, ativo, id da categoria, nome da categoria, imagens |
| `CategoriaDto` | Id, nome, slug, descrição, ativo |
| `ImagemProdutoDto` | Id, URL, ordem |

Os preços e o estoque chegam **prontos no DTO**. O mapeamento acontece na projeção da
consulta, então o frontend nunca manipula entidade do domínio nem precisa saber como o
banco guarda o dado.

`NomeCategoria` vem junto de `ProdutoDto` para evitar uma segunda chamada para
montar o card do produto.

> O preço de **custo** não vai ao DTO — é dado financeiro interno e não deve chegar ao
> navegador.

---

## O que ainda não existe

A API pública é **somente leitura**. A escrita existe, mas atrás de
`api/admin/produtos` e `api/admin/categorias`, protegidas pela policy
`SomenteAdministrador`: criar, editar, ativar/desativar, remover, subir imagem (máximo
de 3 por produto), reordenar e excluir — ver
[`13-integracoes-pendentes.md`](13-integracoes-pendentes.md).

Ainda falta:

- Controle de estoque (entrada, saída e ajuste) — hoje `QuantidadeEstoque` é um número
  editável à mão.
- A tela de administração no frontend.

