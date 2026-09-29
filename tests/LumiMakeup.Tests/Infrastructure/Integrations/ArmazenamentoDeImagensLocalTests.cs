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
    public async Task ArmazenarEmPastaAsync_grava_na_pasta_pedida_e_nao_na_padrao()
    {
        var servico = Criar(pastaPadrao: "produtos");
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarEmPastaAsync(conteudo, "hero.png", "banners", CancellationToken.None);

        Assert.StartsWith("banners/", resultado.CaminhoRelativo);
        Assert.True(File.Exists(Path.Combine(_raiz, resultado.CaminhoRelativo)));
        Assert.False(Directory.Exists(Path.Combine(_raiz, "produtos")));
    }

    [Fact]
    public async Task ArmazenarAsync_continua_usando_a_pasta_padrao()
    {
        // O metodo novo nao pode ter alterado o comportamento antigo: as fotos de
        // produto seguem em produtos/.
        var servico = Criar(pastaPadrao: "produtos");
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarAsync(conteudo, "batom.png", CancellationToken.None);

        Assert.StartsWith("produtos/", resultado.CaminhoRelativo);
    }

    [Fact]
    public async Task ArmazenarEmPastaAsync_aceita_subpasta_interna()
    {
        var servico = Criar();
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarEmPastaAsync(conteudo, "hero.png", "banners/2026", CancellationToken.None);

        Assert.StartsWith("banners/2026/", resultado.CaminhoRelativo);
        Assert.True(File.Exists(Path.Combine(_raiz, resultado.CaminhoRelativo)));
    }

    [Fact]
    public async Task ArmazenarEmPastaAsync_neutraliza_tentativa_de_sair_da_raiz()
    {
        // SanearPasta deixa passar "/" e descarta ".", entao "../../segredo" vira
        // "segredo". O arquivo tem de ficar dentro da raiz, nunca ao lado dela.
        var baseDeTeste = Path.Combine(_raiz, "base");
        Directory.CreateDirectory(baseDeTeste);
        var servico = Criar(caminhoBase: baseDeTeste);
        using var conteudo = ImagemPng();

        var resultado = await servico.ArmazenarEmPastaAsync(conteudo, "hero.png", "../../segredo", CancellationToken.None);

        Assert.StartsWith("segredo/", resultado.CaminhoRelativo);
        Assert.True(File.Exists(Path.Combine(baseDeTeste, resultado.CaminhoRelativo)));
        Assert.False(File.Exists(Path.Combine(_raiz, resultado.CaminhoRelativo)));
    }

    [Fact]
    public async Task ArmazenarEmPastaAsync_recusa_pasta_vazia()
    {
        var servico = Criar();
        using var conteudo = ImagemPng();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarEmPastaAsync(conteudo, "hero.png", "   ", CancellationToken.None));
    }

    [Fact]
    public async Task ArmazenarEmPastaAsync_aplica_o_mesmo_limite_de_tamanho()
    {
        var servico = Criar(tamanhoMaximo: 64);
        using var conteudo = ImagemPng(bytesExtras: 512);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarEmPastaAsync(conteudo, "hero.png", "banners", CancellationToken.None));
    }

    [Fact]
    public async Task ArmazenarEmPastaAsync_recusa_formato_nao_permitido()
    {
        var servico = Criar(extensoes: ["png"]);
        using var conteudo = ImagemJpeg();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ArmazenarEmPastaAsync(conteudo, "hero.jpg", "banners", CancellationToken.None));
    }

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

        // A mensagem e generica de proposito: a mesma validacao agora protege
        // tambem a pasta pedida em ArmazenarEmPastaAsync, onde dizer
        // "PastaPadrao" apontaria para a opcao que o chamador nem usou.
        Assert.Contains("pasta de destino", excecao.Message);
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
    public async Task ExcluirAsync_nao_falha_com_path_traversal()
    {
        var servico = Criar();

        // Apagar o arquivo e melhor esforco; apagar a referencia no banco e o que a
        // pessoa pediu. Quando o caminho guardado e invalido, lancar aqui impedia a
        // remocao e deixava o registro orfao no banco, sem tela onde pudesse ser
        // limpo. O caminho continua sendo validado, so nao derruba a operacao.
        await servico.ExcluirAsync("../../segredo.txt", CancellationToken.None);
    }

    [Fact]
    public async Task ExcluirAsync_nao_falha_com_caminho_absoluto_fora_da_raiz()
    {
        var servico = Criar();

        // O caminho tem que ser absoluto **para o sistema que roda o teste**. Um literal
        // "C:/Windows/..." nao e absoluto no Linux: la vira um simples nome de pasta, o
        // Path.Combine mantem a raiz, o prefixo bate e a excecao nunca vem. Montar com
        // Path.GetTempPath() da um caminho absoluto real nos dois sistemas, que e o que
        // a protecao precisa rejeitar.
        var foraDaRaiz = Path.Combine(
            Path.GetTempPath(),
            $"fora-da-raiz-{Guid.NewGuid():N}.txt");

        await servico.ExcluirAsync(foraDaRaiz, CancellationToken.None);
    }

    [Fact]
    public async Task ExcluirAsync_remove_o_arquivo_quando_o_caminho_e_valido()
    {
        var raiz = Path.Combine(
            Path.GetTempPath(),
            "lumi-exclusao-valida",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(raiz, "produtos"));

        try
        {
            var arquivo = Path.Combine(raiz, "produtos", "existe.jpg");
            await File.WriteAllTextAsync(arquivo, "conteudo");

            var opcoes = Options.Create(new ArmazenamentoDeImagensOptions
            {
                CaminhoBase = raiz,
                PastaPadrao = "produtos",
                TamanhoMaximoEmBytes = 5_242_880,
                ExtensoesPermitidas = new[] { "jpg", "jpeg", "png" }
            });
            var servico = new ArmazenamentoDeImagensLocal(
                opcoes,
                LumiMakeup.Tests.Helpers.Testes.CriarAmbiente(AppContext.BaseDirectory),
                NullLogger<ArmazenamentoDeImagensLocal>.Instance);

            await servico.ExcluirAsync("produtos/existe.jpg", CancellationToken.None);

            Assert.False(File.Exists(arquivo));
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }
}
