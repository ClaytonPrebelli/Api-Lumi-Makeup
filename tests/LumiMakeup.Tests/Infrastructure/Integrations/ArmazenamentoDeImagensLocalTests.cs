using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public sealed class ArmazenamentoDeImagensLocalTests : IDisposable
{
    private static readonly byte[] CabecalhoPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] CabecalhoJpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    private static readonly byte[] CabecalhoGif = { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00 };

    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(),
        "lumi-imagens-testes",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private ArmazenamentoDeImagensLocal Criar(
        string? caminhoBase = null,
        long tamanhoMaximo = 5_242_880,
        string[]? extensoes = null,
        string? pastaPadrao = null)
    {
        var opcoes = Options.Create(new ArmazenamentoDeImagensOptions
        {
            CaminhoBase = caminhoBase ?? _raiz,
            PastaPadrao = pastaPadrao ?? "produtos",
            TamanhoMaximoEmBytes = tamanhoMaximo,
            ExtensoesPermitidas = extensoes ?? new[] { "jpg", "jpeg", "png" }
        });

        return new ArmazenamentoDeImagensLocal(
            opcoes,
            Testes.CriarAmbiente(AppContext.BaseDirectory),
            NullLogger<ArmazenamentoDeImagensLocal>.Instance);
    }

    private static MemoryStream Conteudo(params byte[] bytes) => new(bytes);

    private static MemoryStream ImagemPng(int bytesExtras = 64) =>
        new([.. CabecalhoPng, .. Enumerable.Repeat((byte)0x20, bytesExtras)]);

    private static MemoryStream ImagemJpeg(int bytesExtras = 64) =>
        new([.. CabecalhoJpeg, .. Enumerable.Repeat((byte)0x11, bytesExtras)]);

    [Fact]
    public void Construtor_lanca_quando_caminho_base_nao_configurado()
    {
        var opcoes = Options.Create(new ArmazenamentoDeImagensOptions { CaminhoBase = "  " });

        var excecao = Assert.Throws<InvalidOperationException>(() => new ArmazenamentoDeImagensLocal(
            opcoes,
            Testes.CriarAmbiente(AppContext.BaseDirectory),
            NullLogger<ArmazenamentoDeImagensLocal>.Instance));

        Assert.Contains("CaminhoBase", excecao.Message);
    }

    [Fact]
    public async Task ArmazenarAsync_grava_png_e_devolve_caminho_relativo()
    {
        var servico = Criar();
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarAsync(conteudo, "batom.png", CancellationToken.None);

        Assert.StartsWith("produtos/", resultado.CaminhoRelativo);
        Assert.EndsWith(".png", resultado.CaminhoRelativo);
        Assert.Equal("image/png", resultado.ContentType);
        Assert.Equal("batom.png", resultado.NomeOriginal);
        Assert.Equal(72, resultado.TamanhoEmBytes);
        Assert.True(File.Exists(Path.Combine(_raiz, resultado.CaminhoRelativo.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task ArmazenarAsync_grava_jpeg_com_extensao_jpg()
    {
        var servico = Criar();
        using var conteudo = ImagemJpeg();

        var resultado = await servico.ArmazenarAsync(conteudo, "base.jpg", CancellationToken.None);

        Assert.EndsWith(".jpg", resultado.CaminhoRelativo);
        Assert.Equal("image/jpeg", resultado.ContentType);
    }

    [Fact]
    public async Task ArmazenarAsync_ignora_extensao_declarada_e_usa_a_do_conteudo()
    {
        var servico = Criar();
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarAsync(conteudo, " Mentira.exe ", CancellationToken.None);

        Assert.EndsWith(".png", resultado.CaminhoRelativo);
        Assert.Equal("Mentira.exe", resultado.NomeOriginal);
    }

    [Fact]
    public async Task ArmazenarAsync_rejeita_conteudo_que_nao_e_jpg_nem_png()
    {
        var servico = Criar();
        using var conteudo = Conteudo([.. CabecalhoGif, .. Enumerable.Repeat((byte)0x00, 32)]);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarAsync(conteudo, "animacao.gif", CancellationToken.None));

        Assert.Contains("não corresponde", excecao.Message);
        Assert.Empty(Directory.Exists(Path.Combine(_raiz, "produtos")) ? Directory.GetFiles(Path.Combine(_raiz, "produtos")) : []);
    }

    [Fact]
    public async Task ArmazenarAsync_rejeita_formato_nao_listado_em_ExtensoesPermitidas()
    {
        var servico = Criar(extensoes: new[] { "png" });
        using var conteudo = ImagemJpeg();

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarAsync(conteudo, "base.jpg", CancellationToken.None));

        Assert.Contains("Formato não permitido", excecao.Message);
    }

    [Fact]
    public async Task ArmazenarAsync_rejeita_arquivo_menor_que_o_cabecalho()
    {
        var servico = Criar();
        using var conteudo = Conteudo(0x89, 0x50);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarAsync(conteudo, "truncado.png", CancellationToken.None));

        Assert.Contains("corrompido", excecao.Message);
    }

    [Fact]
    public async Task ArmazenarAsync_rejeita_arquivo_acima_do_limite_e_nao_deixa_arquivo()
    {
        var servico = Criar(tamanhoMaximo: 16);
        using var conteudo = ImagemPng(512);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarAsync(conteudo, "grande.png", CancellationToken.None));

        Assert.Contains("excede o limite", excecao.Message);
        Assert.Empty(Directory.Exists(Path.Combine(_raiz, "produtos")) ? Directory.GetFiles(Path.Combine(_raiz, "produtos")) : []);
    }

    [Fact]
    public async Task ArmazenarAsync_rejeita_conteudo_nulo()
    {
        var servico = Criar();

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarAsync(null!, "foto.png", CancellationToken.None));

        Assert.Contains("Nenhuma imagem", excecao.Message);
    }

    [Fact]
    public async Task ArmazenarAsync_gera_nomes_unicos_para_o_mesmo_arquivo()
    {
        var servico = Criar();
        using var primeiro = ImagemPng();
        using var segundo = ImagemPng();

        var a = await servico.ArmazenarAsync(primeiro, "igual.png", CancellationToken.None);
        var b = await servico.ArmazenarAsync(segundo, "igual.png", CancellationToken.None);

        Assert.NotEqual(a.CaminhoRelativo, b.CaminhoRelativo);
    }

    [Fact]
    public async Task ArmazenarAsync_resolve_caminho_base_relativo_ao_content_root()
    {
        var servico = Criar(caminhoBase: "imagens-relativas");
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarAsync(conteudo, "foto.png", CancellationToken.None);

        var esperado = Path.Combine(AppContext.BaseDirectory, "imagens-relativas", resultado.CaminhoRelativo);
        Assert.True(File.Exists(esperado), $"arquivo ausente em {esperado}");
        Directory.Delete(Path.Combine(AppContext.BaseDirectory, "imagens-relativas"), recursive: true);
    }

    [Fact]
    public async Task ArmazenarAsync_rejeita_pasta_padrao_invalida()
    {
        var servico = Criar(pastaPadrao: "..");

        using var conteudo = ImagemPng();
        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarAsync(conteudo, "foto.png", CancellationToken.None));

        Assert.Contains("PastaPadrao", excecao.Message);
    }

    [Fact]
    public async Task ArmazenarAsync_saneara_nome_original_com_caracteres_indevidos()
    {
        var servico = Criar();
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarAsync(conteudo, " ../../etc/pas*sw*d  ", CancellationToken.None);

        Assert.Equal("passwd", resultado.NomeOriginal);
    }

    [Fact]
    public async Task ArmazenarAsync_usa_imagem_padrao_quando_nome_original_vazio()
    {
        var servico = Criar();
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarAsync(conteudo, "***", CancellationToken.None);

        Assert.Equal("imagem", resultado.NomeOriginal);
    }

    [Fact]
    public async Task ExcluirAsync_remove_o_arquivo_do_disco()
    {
        var servico = Criar();
        using var conteudo = ImagemPng();
        var armazenada = await servico.ArmazenarAsync(conteudo, "foto.png", CancellationToken.None);
        var caminho = Path.Combine(_raiz, armazenada.CaminhoRelativo.Replace('/', Path.DirectorySeparatorChar));

        await servico.ExcluirAsync(armazenada.CaminhoRelativo, CancellationToken.None);

        Assert.False(File.Exists(caminho));
    }

    [Fact]
    public async Task ExcluirAsync_nao_falha_quando_o_arquivo_nao_existe()
    {
        var servico = Criar();

        await servico.ExcluirAsync("produtos/nao-existe.png", CancellationToken.None);
    }

    [Fact]
    public async Task ExcluirAsync_ignora_caminho_vazio()
    {
        var servico = Criar();

        await servico.ExcluirAsync("   ", CancellationToken.None);
    }

    [Fact]
    public async Task ExcluirAsync_rejeita_path_traversal()
    {
        var servico = Criar();

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ExcluirAsync("../../segredo.txt", CancellationToken.None));

        Assert.Contains("inválido", excecao.Message);
    }

    [Fact]
    public async Task ExcluirAsync_rejeita_caminho_absoluto_fora_da_raiz()
    {
        var servico = Criar();

        // O caminho tem que ser absoluto **para o sistema que roda o teste**.Um literal
        // "C:/Windows/..." nao e absoluto no Linux: la vira um simples nome de pasta, o
        // Path.Combine mantem a raiz, o prefixo bate e a excecao nunca vem. Montar com
        // Path.GetTempPath() da um caminho absoluto real nos dois sistemas, que e o que
        // a protecao precisa rejeitar.
        var foraDaRaiz = Path.Combine(
            Path.GetTempPath(),
            $"fora-da-raiz-{Guid.NewGuid():N}.txt");

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ExcluirAsync(foraDaRaiz, CancellationToken.None));

        Assert.Contains("inválido", excecao.Message);
    }
}
