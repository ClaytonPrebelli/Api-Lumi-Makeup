using LumiMakeup.Application.Abstractions;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeBannersServiceTests
{
    private static async Task<Banner> SemearAsync(
        LumiDbContext contexto,
        int ordem = 0,
        bool ativo = true)
    {
        var banner = new Banner
        {
            CaminhoRelativoDesktop = $"banners/desktop{ordem}.png",
            CaminhoRelativoMobile = $"banners/mobile{ordem}.png",
            NomeOriginalDesktop = $"desktop{ordem}.png",
            NomeOriginalMobile = $"mobile{ordem}.png",
            Ordem = ordem,
            Ativo = ativo
        };

        contexto.Banners.Add(banner);
        await contexto.SaveChangesAsync();
        return banner;
    }

    private static Mock<IArmazenamentoDeImagens> ArmazenamentoQueGrava()
    {
        var mock = new Mock<IArmazenamentoDeImagens>();

        mock.Setup(a => a.ArmazenarEmPastaAsync(
                It.IsAny<Stream>(), "desktop.png", "banners", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImagemArmazenada("banners/d1.png", "desktop.png", "image/png", 10));

        mock.Setup(a => a.ArmazenarEmPastaAsync(
                It.IsAny<Stream>(), "mobile.png", "banners", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImagemArmazenada("banners/m1.png", "mobile.png", "image/png", 10));

        return mock;
    }

    [Fact]
    public async Task ObterTodosAsync_traz_ativos_e_inativos_ordenados_por_ordem()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, ordem: 2);
        await SemearAsync(contexto, ordem: 0, ativo: false);
        await SemearAsync(contexto, ordem: 1);
        var servico = new GestaoDeBannersService(contexto, ArmazenamentoQueGrava().Object);

        var banners = await servico.ObterTodosAsync(CancellationToken.None);

        Assert.Equal(3, banners.Count);
        Assert.Equal(new[] { 0, 1, 2 }, banners.Select(b => b.Ordem));
    }

    [Fact]
    public async Task ObterAtivosAsync_esconde_banner_desativado()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, ordem: 0, ativo: false);
        await SemearAsync(contexto, ordem: 1, ativo: true);
        var servico = new GestaoDeBannersService(contexto, ArmazenamentoQueGrava().Object);

        var banners = await servico.ObterAtivosAsync(CancellationToken.None);

        Assert.Single(banners);
        Assert.Equal(1, banners[0].Ordem);
    }

    [Fact]
    public async Task CriarAsync_grava_as_duas_imagens_na_pasta_de_banners()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var armazenamento = ArmazenamentoQueGrava();
        var servico = new GestaoDeBannersService(contexto, armazenamento.Object);
        using var desktop = new MemoryStream([1]);
        using var mobile = new MemoryStream([2]);

        var banner = await servico.CriarAsync(desktop, "desktop.png", mobile, "mobile.png", " Coleção nova ", CancellationToken.None);

        Assert.Equal("banners/d1.png", banner.CaminhoRelativoDesktop);
        Assert.Equal("banners/m1.png", banner.CaminhoRelativoMobile);
        Assert.Equal("Coleção nova", banner.TextoAlternativo);
        Assert.True(banner.Ativo);
        Assert.Single(contexto.Banners.ToList());

        armazenamento.Verify(a => a.ArmazenarEmPastaAsync(
            It.IsAny<Stream>(), "desktop.png", "banners", It.IsAny<CancellationToken>()), Times.Once);
        armazenamento.Verify(a => a.ArmazenarEmPastaAsync(
            It.IsAny<Stream>(), "mobile.png", "banners", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_coloca_o_novo_apos_o_ultimo_ja_existente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, ordem: 0);
        await SemearAsync(contexto, ordem: 1);
        var servico = new GestaoDeBannersService(contexto, ArmazenamentoQueGrava().Object);
        using var desktop = new MemoryStream([1]);
        using var mobile = new MemoryStream([2]);

        var banner = await servico.CriarAsync(desktop, "desktop.png", mobile, "mobile.png", null, CancellationToken.None);

        Assert.Equal(2, banner.Ordem);
    }

    [Fact]
    public async Task CriarAsync_bloqueia_o_quarto_banner()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, ordem: 0);
        await SemearAsync(contexto, ordem: 1);
        await SemearAsync(contexto, ordem: 2, ativo: false);
        var servico = new GestaoDeBannersService(contexto, ArmazenamentoQueGrava().Object);
        using var desktop = new MemoryStream([1]);
        using var mobile = new MemoryStream([2]);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(desktop, "desktop.png", mobile, "mobile.png", null, CancellationToken.None));

        Assert.Contains("3 banners", erro.Message);
    }

    /// <summary>
    /// O desktop ja estava gravado quando o mobile foi recusado. Sem a limpeza,
    /// sobraria um arquivo em banners/ que ninguem no banco conhece — e que o
    /// painel nao tem como listar, porque so o cadastro know o caminho.
    /// </summary>
    [Fact]
    public async Task CriarAsync_apaga_o_desktop_ja_gravado_quando_o_mobile_falha()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var armazenamento = ArmazenamentoQueGrava();
        armazenamento
            .Setup(a => a.ArmazenarEmPastaAsync(
                It.IsAny<Stream>(), "mobile.png", "banners", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Formato não permitido."));

        var servico = new GestaoDeBannersService(contexto, armazenamento.Object);
        using var desktop = new MemoryStream([1]);
        using var mobile = new MemoryStream([2]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(desktop, "desktop.png", mobile, "mobile.png", null, CancellationToken.None));

        armazenamento.Verify(a => a.ExcluirAsync("banners/d1.png", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(contexto.Banners.ToList());
    }

    [Fact]
    public async Task CriarAsync_apaga_as_duas_imagens_quando_o_banco_recusa()
    {
        // Contexto que sobe no Add mas quebra no SaveChanges, para simular o banco
        // recusando a gravacao depois que os dois arquivos ja sairam do disco.
        using var contexto = new ContextoQueRecusaGravacao();
        var armazenamento = ArmazenamentoQueGrava();
        var servico = new GestaoDeBannersService(contexto, armazenamento.Object);
        using var desktop = new MemoryStream([1]);
        using var mobile = new MemoryStream([2]);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            servico.CriarAsync(desktop, "desktop.png", mobile, "mobile.png", null, CancellationToken.None));

        // Sem esta limpeza, banners/ guardaria dois arquivos sem nenhuma linha no
        // banco apontando para eles.
        armazenamento.Verify(a => a.ExcluirAsync("banners/d1.png", It.IsAny<CancellationToken>()), Times.Once);
        armazenamento.Verify(a => a.ExcluirAsync("banners/m1.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AtualizarAsync_trocando_uma_imagem_mantem_a_outra_e_apaga_a_velha()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var banner = await SemearAsync(contexto);
        var caminhoMobileOriginal = banner.CaminhoRelativoMobile;

        var armazenamento = ArmazenamentoQueGrava();
        var servico = new GestaoDeBannersService(contexto, armazenamento.Object);
        using var nova = new MemoryStream([1]);

        var atualizado = await servico.AtualizarAsync(
            banner.Id, nova, "desktop.png", null, null, null, CancellationToken.None);

        Assert.Equal("banners/d1.png", atualizado.CaminhoRelativoDesktop);
        Assert.Equal(caminhoMobileOriginal, atualizado.CaminhoRelativoMobile);

        // So o desktop antigo sai: o mobile continua em uso pelo mesmo slide.
        armazenamento.Verify(a => a.ExcluirAsync("banners/desktop0.png", It.IsAny<CancellationToken>()), Times.Once);
        armazenamento.Verify(a => a.ExcluirAsync(caminhoMobileOriginal, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AtualizarAsync_sem_arquivo_altera_so_o_texto_alternativo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var banner = await SemearAsync(contexto);
        var caminhoDesktopOriginal = banner.CaminhoRelativoDesktop;
        var caminhoMobileOriginal = banner.CaminhoRelativoMobile;

        var armazenamento = ArmazenamentoQueGrava();
        var servico = new GestaoDeBannersService(contexto, armazenamento.Object);

        var atualizado = await servico.AtualizarAsync(
            banner.Id, null, null, null, null, "Acesso pela esquerda", CancellationToken.None);

        Assert.Equal("Acesso pela esquerda", atualizado.TextoAlternativo);
        Assert.Equal(caminhoDesktopOriginal, atualizado.CaminhoRelativoDesktop);
        Assert.Equal(caminhoMobileOriginal, atualizado.CaminhoRelativoMobile);

        armazenamento.Verify(
            a => a.ArmazenarEmPastaAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        armazenamento.Verify(a => a.ExcluirAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AtualizarAsync_rejeita_id_inexistente_sem_gravar_nada()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var armazenamento = ArmazenamentoQueGrava();
        var servico = new GestaoDeBannersService(contexto, armazenamento.Object);
        using var nova = new MemoryStream([1]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.AtualizarAsync(999, nova, "desktop.png", null, null, null, CancellationToken.None));

        armazenamento.Verify(
            a => a.ArmazenarEmPastaAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DefinirAtivoAsync_alterna_o_banner_sem_tocar_nas_imagens()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var banner = await SemearAsync(contexto, ativo: true);
        var armazenamento = ArmazenamentoQueGrava();
        var servico = new GestaoDeBannersService(contexto, armazenamento.Object);

        var desativado = await servico.DefinirAtivoAsync(banner.Id, false, CancellationToken.None);

        Assert.False(desativado.Ativo);
        Assert.Equal("banners/desktop0.png", desativado.CaminhoRelativoDesktop);
        armazenamento.Verify(a => a.ExcluirAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExcluirAsync_deruba_a_linha_e_as_duas_imagens()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var banner = await SemearAsync(contexto);
        var armazenamento = ArmazenamentoQueGrava();
        var servico = new GestaoDeBannersService(contexto, armazenamento.Object);

        await servico.ExcluirAsync(banner.Id, CancellationToken.None);

        Assert.Empty(contexto.Banners.ToList());
        armazenamento.Verify(a => a.ExcluirAsync("banners/desktop0.png", It.IsAny<CancellationToken>()), Times.Once);
        armazenamento.Verify(a => a.ExcluirAsync("banners/mobile0.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExcluirAsync_rejeita_id_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeBannersService(contexto, ArmazenamentoQueGrava().Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.ExcluirAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task ReordenarAsync_grava_a_posicao_de_cada_banner()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var primeiro = await SemearAsync(contexto, ordem: 0);
        var segundo = await SemearAsync(contexto, ordem: 1);
        var servico = new GestaoDeBannersService(contexto, ArmazenamentoQueGrava().Object);

        var banners = await servico.ReordenarAsync([segundo.Id, primeiro.Id], CancellationToken.None);

        Assert.Equal(new[] { segundo.Id, primeiro.Id }, banners.Select(b => b.Id));
        Assert.Equal(new[] { 0, 1 }, banners.Select(b => b.Ordem));
    }

    [Fact]
    public async Task ReordenarAsync_recusa_lista_parcial()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var primeiro = await SemearAsync(contexto, ordem: 0);
        await SemearAsync(contexto, ordem: 1);
        var servico = new GestaoDeBannersService(contexto, ArmazenamentoQueGrava().Object);

        // Mandar so um id deixaria o outro com a ordem antiga, e o painel mostraria
        // uma sequencia diferente da gravada.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ReordenarAsync([primeiro.Id], CancellationToken.None));

        Assert.Contains("não corresponde", erro.Message);
    }

    [Fact]
    public async Task ReordenarAsync_recusa_id_que_nao_existe()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var primeiro = await SemearAsync(contexto, ordem: 0);
        var servico = new GestaoDeBannersService(contexto, ArmazenamentoQueGrava().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ReordenarAsync([primeiro.Id, 999], CancellationToken.None));
    }

    /// <summary>
    /// Aceita o Add e falha no SaveChanges. É o cenário que o provedor InMemory nao
    /// produz sozinho, e é justamente o que deixa arquivo órfão em disco.
    /// </summary>
    private sealed class ContextoQueRecusaGravacao : LumiDbContext
    {
        public ContextoQueRecusaGravacao()
            : base(new DbContextOptionsBuilder<LumiDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Gravacao recusada pelo banco.");
    }
}
