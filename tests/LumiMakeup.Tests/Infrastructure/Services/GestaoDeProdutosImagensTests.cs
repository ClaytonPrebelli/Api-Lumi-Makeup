using LumiMakeup.Application.Abstractions;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Services;

/// <summary>
/// Exclusão de imagem contra o armazenamento de verdade, e não o mock.
///
/// O resto da suíte usa <c>Mock.Of&lt;IArmazenamentoDeImagens&gt;()</c>, o que
/// nunca chega a exercitar o disco. Foi exatamente esse caminho que ficou sem
/// cobertura: uma referência de imagem cujo arquivo não existe mais no servidor
/// precisa poder ser apagada, porque o arquivo pode ter sido perdido no deploy,
/// removido na mão, ou nunca ter subido para aquele ambiente.
/// </summary>
public sealed class GestaoDeProdutosImagensTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(),
        "lumi-imagens-exclusao",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static ArmazenamentoDeImagensLocal CriarArmazenamento(string raiz)
    {
        var opcoes = Options.Create(new ArmazenamentoDeImagensOptions
        {
            CaminhoBase = raiz,
            PastaPadrao = "produtos",
            TamanhoMaximoEmBytes = 5_242_880,
            ExtensoesPermitidas = new[] { "jpg", "jpeg", "png" }
        });

        return new ArmazenamentoDeImagensLocal(
            opcoes,
            Testes.CriarAmbiente(AppContext.BaseDirectory),
            NullLogger<ArmazenamentoDeImagensLocal>.Instance);
    }

    private static async Task<Produto> SemearProdutoComImagemAsync(LumiDbContext contexto, string caminhoRelativo)
    {
        var categoria = new Categoria { Nome = "Batom", Slug = "batom", Ativo = true };
        var produto = new Produto
        {
            Nome = "Batom Matte",
            Slug = "batom-matte",
            Descricao = "Batom de alta duração",
            PrecoCusto = 20m,
            PrecoVenda = 39.90m,
            QuantidadeEstoque = 5,
            Ativo = true,
            Categoria = categoria,
            Imagens =
            {
                new ImagemProduto
                {
                    CaminhoRelativo = caminhoRelativo,
                    NomeOriginal = "batom.jpg",
                    Ordem = 0
                }
            }
        };

        contexto.Produtos.Add(produto);
        await contexto.SaveChangesAsync();
        return produto;
    }

    [Fact]
    public async Task ExcluirImagemAsync_remove_a_referencia_mesmo_sem_o_arquivo_no_disco()
    {
        using var contexto = Testes.CriarContextoInMemory();
        // A pasta existe, mas o arquivo dentro dela nunca foi gravado.
        Directory.CreateDirectory(Path.Combine(_raiz, "produtos"));
        var produto = await SemearProdutoComImagemAsync(contexto, "produtos/nao-existe.jpg");
        var imagemId = produto.Imagens.Single().Id;

        var servico = new GestaoDeProdutosService(contexto, CriarArmazenamento(_raiz));

        await servico.ExcluirImagemAsync(produto.Id, imagemId);

        Assert.Empty(await contexto.ImagensProduto.Where(i => i.ProdutoId == produto.Id).ToListAsync());
    }

    [Fact]
    public async Task ExcluirAsync_do_produto_ignora_arquivos_ausentes()
    {
        using var contexto = Testes.CriarContextoInMemory();
        Directory.CreateDirectory(Path.Combine(_raiz, "produtos"));
        var produto = await SemearProdutoComImagemAsync(contexto, "produtos/nao-existe.jpg");

        var servico = new GestaoDeProdutosService(contexto, CriarArmazenamento(_raiz));

        await servico.ExcluirAsync(produto.Id);

        Assert.Empty(await contexto.Produtos.ToListAsync());
    }

    [Fact]
    public async Task ExcluirImagemAsync_remove_a_referencia_com_caminho_que_aponta_para_fora_da_raiz()
    {
        using var contexto = Testes.CriarContextoInMemory();
        Directory.CreateDirectory(Path.Combine(_raiz, "produtos"));

        // Caminho com ".." escapa da raiz e o ResolverCaminhoSeguro rejeita. Sem o
        // tratamento, essa excecao impedia a remocao e deixava o registro orfao
        // no banco, sem tela onde pudesse ser limpo.
        var produto = await SemearProdutoComImagemAsync(contexto, "produtos/../../fora.jpg");
        var imagemId = produto.Imagens.Single().Id;

        var servico = new GestaoDeProdutosService(contexto, CriarArmazenamento(_raiz));

        await servico.ExcluirImagemAsync(produto.Id, imagemId);

        Assert.Empty(await contexto.ImagensProduto.Where(i => i.ProdutoId == produto.Id).ToListAsync());
    }

    [Fact]
    public async Task ExcluirAsync_de_produto_com_duas_imagens_sem_arquivo_apaga_as_duas_referencias()
    {
        using var contexto = Testes.CriarContextoInMemory();
        Directory.CreateDirectory(Path.Combine(_raiz, "produtos"));

        // O cenario relatado: produto com duas fotos que so existem localmente, e
        // portanto nao estao no disco do outro ambiente.
        var produto = await SemearProdutoComImagemAsync(contexto, "produtos/primeira.jpg");
        var segunda = new ImagemProduto
        {
            ProdutoId = produto.Id,
            CaminhoRelativo = "produtos/segunda.jpg",
            NomeOriginal = "segunda.jpg",
            Ordem = 1
        };
        contexto.ImagensProduto.Add(segunda);
        await contexto.SaveChangesAsync();

        var servico = new GestaoDeProdutosService(contexto, CriarArmazenamento(_raiz));

        await servico.ExcluirAsync(produto.Id);

        Assert.Empty(await contexto.Produtos.ToListAsync());
        Assert.Empty(await contexto.ImagensProduto.ToListAsync());
    }

    [Fact]
    public async Task ExcluirImagemAsync_remove_o_arquivo_quando_existe()
    {
        using var contexto = Testes.CriarContextoInMemory();
        Directory.CreateDirectory(Path.Combine(_raiz, "produtos"));
        var arquivo = Path.Combine(_raiz, "produtos", "existe.jpg");
        await File.WriteAllTextAsync(arquivo, "conteudo");

        var produto = await SemearProdutoComImagemAsync(contexto, "produtos/existe.jpg");
        var imagemId = produto.Imagens.Single().Id;

        var servico = new GestaoDeProdutosService(contexto, CriarArmazenamento(_raiz));

        await servico.ExcluirImagemAsync(produto.Id, imagemId);

        Assert.False(File.Exists(arquivo));
        Assert.Empty(await contexto.ImagensProduto.Where(i => i.ProdutoId == produto.Id).ToListAsync());
    }
}
