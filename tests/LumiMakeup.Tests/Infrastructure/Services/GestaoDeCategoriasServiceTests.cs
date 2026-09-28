using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeCategoriasServiceTests
{
    private static async Task<Categoria> SemearAsync(LumiDbContext contexto, string nome = "Batom", string slug = "batom", bool ativa = true)
    {
        var categoria = new Categoria { Nome = nome, Slug = slug, Ativo = ativa };
        contexto.Categorias.Add(categoria);
        await contexto.SaveChangesAsync();
        return categoria;
    }

    [Fact]
    public async Task ObterTodasAsync_traz_ativas_e_inativas_ordenadas_por_nome()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "Máscara", "mascara");
        await SemearAsync(contexto, "Batom", "batom");
        await SemearAsync(contexto, "Apagada", "apagada", ativa: false);
        var servico = new GestaoDeCategoriasService(contexto);

        var categorias = await servico.ObterTodasAsync(CancellationToken.None);

        Assert.Equal(3, categorias.Count);
        Assert.Equal(new[] { "Apagada", "Batom", "Máscara" }, categorias.Select(c => c.Nome));
    }

    [Fact]
    public async Task CriarAsync_gera_slug_e_persiste()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeCategoriasService(contexto);

        var categoria = await servico.CriarAsync(
            new RequisicaoDeCategoria("Base Líquida", null, "Maquiagem de alta cobertura", true),
            CancellationToken.None);

        Assert.Equal("base-liquida", categoria.Slug);
        Assert.Equal("Maquiagem de alta cobertura", categoria.Descricao);
        Assert.True(categoria.Ativo);
        Assert.Single(contexto.Categorias.ToList());
    }

    [Fact]
    public async Task CriarAsync_guarda_descricao_nula_quando_nao_informada()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeCategoriasService(contexto);

        var categoria = await servico.CriarAsync(
            new RequisicaoDeCategoria("Delineador", null, "   ", true),
            CancellationToken.None);

        Assert.Null(categoria.Descricao);
    }

    [Fact]
    public async Task CriarAsync_desambigua_slug_duplicado()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto);
        var servico = new GestaoDeCategoriasService(contexto);

        var categoria = await servico.CriarAsync(
            new RequisicaoDeCategoria("Batom", "batom", null, true),
            CancellationToken.None);

        Assert.Equal("batom-2", categoria.Slug);
    }

    [Fact]
    public async Task CriarAsync_usa_slug_padrao_quando_nome_nao_gera_texto_util()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeCategoriasService(contexto);

        var categoria = await servico.CriarAsync(
            new RequisicaoDeCategoria("---", null, null, true),
            CancellationToken.None);

        Assert.Equal("categoria", categoria.Slug);
    }

    [Fact]
    public async Task CriarAsync_exige_nome()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeCategoriasService(contexto);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(new RequisicaoDeCategoria("  ", null, null, true), CancellationToken.None));

        Assert.Contains("nome", excecao.Message);
    }

    [Fact]
    public async Task AtualizarAsync_altera_campos_da_categoria()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearAsync(contexto);
        var servico = new GestaoDeCategoriasService(contexto);

        var atualizada = await servico.AtualizarAsync(
            categoria.Id,
            new RequisicaoDeCategoria("Batom Premium", "batom-premium", "Linha nova", false),
            CancellationToken.None);

        Assert.Equal("Batom Premium", atualizada.Nome);
        Assert.Equal("batom-premium", atualizada.Slug);
        Assert.Equal("Linha nova", atualizada.Descricao);
        Assert.False(atualizada.Ativo);
    }

    [Fact]
    public async Task AtualizarAsync_lanca_para_categoria_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeCategoriasService(contexto);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.AtualizarAsync(999, new RequisicaoDeCategoria("X", null, null, true), CancellationToken.None));
    }

    [Fact]
    public async Task ExcluirAsync_remove_categoria_sem_produtos()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearAsync(contexto);
        var servico = new GestaoDeCategoriasService(contexto);

        await servico.ExcluirAsync(categoria.Id, CancellationToken.None);

        Assert.Empty(contexto.Categorias.ToList());
    }

    [Fact]
    public async Task ExcluirAsync_recusa_categoria_com_produtos_vinculados()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearAsync(contexto);
        contexto.Produtos.Add(new Produto
        {
            Nome = "Batom", Slug = "batom-x", Descricao = "", CategoriaId = categoria.Id, Categoria = categoria
        });
        await contexto.SaveChangesAsync();
        var servico = new GestaoDeCategoriasService(contexto);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ExcluirAsync(categoria.Id, CancellationToken.None));

        Assert.Contains("produtos vinculados", excecao.Message);
    }

    [Fact]
    public async Task ExcluirAsync_lanca_para_categoria_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeCategoriasService(contexto);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.ExcluirAsync(999, CancellationToken.None));
    }
}
