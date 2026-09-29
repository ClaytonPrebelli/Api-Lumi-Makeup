using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeBannersService : IGestaoDeBannersService
{
    /// <summary>Subpasta do armazenamento onde as artes do hero ficam gravadas.</summary>
    private const string PastaDosBanners = "banners";

    private readonly LumiDbContext _contexto;
    private readonly IArmazenamentoDeImagens _armazenamento;

    public GestaoDeBannersService(LumiDbContext contexto, IArmazenamentoDeImagens armazenamento)
    {
        _contexto = contexto;
        _armazenamento = armazenamento;
    }

    public async Task<IReadOnlyList<BannerAdministracaoDto>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Banners
            .AsNoTracking()
            .OrderBy(b => b.Ordem)
            .ThenBy(b => b.Id)
            .Select(b => ParaAdministracao(b))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BannerDto>> ObterAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Banners
            .AsNoTracking()
            .Where(b => b.Ativo)
            .OrderBy(b => b.Ordem)
            .ThenBy(b => b.Id)
            .Select(b => new BannerDto(
                b.Id,
                b.CaminhoRelativoDesktop,
                b.CaminhoRelativoMobile,
                b.TextoAlternativo,
                b.Ordem))
            .ToListAsync(cancellationToken);
    }

    public async Task<BannerAdministracaoDto> CriarAsync(
        Stream imagemDesktop,
        string nomeOriginalDesktop,
        Stream imagemMobile,
        string nomeOriginalMobile,
        string? textoAlternativo,
        CancellationToken cancellationToken = default)
    {
        var totalAtual = await _contexto.Banners.CountAsync(cancellationToken);

        if (totalAtual >= IGestaoDeBannersService.MaximoDeBanners)
        {
            throw new InvalidOperationException(
                $"O carrossel aceita no máximo {IGestaoDeBannersService.MaximoDeBanners} banners. Exclua um existente ou desative-o para liberar a posição.");
        }

        // Grava o desktop primeiro e o mobile depois. Se o segundo upload falhar
        // (formato recusado, arquivo grande demais), o primeiro ja gravado e
        // apagado aqui: sem isso sobraria um arquivo orfao na pasta, sem
        // registro no banco e sem como o painel limpa-lo depois.
        var desktop = await _armazenamento.ArmazenarEmPastaAsync(
            imagemDesktop, nomeOriginalDesktop, PastaDosBanners, cancellationToken);

        ImagemArmazenada mobile;
        try
        {
            mobile = await _armazenamento.ArmazenarEmPastaAsync(
                imagemMobile, nomeOriginalMobile, PastaDosBanners, cancellationToken);
        }
        catch
        {
            await ExcluirArquivosAsync(desktop.CaminhoRelativo, null, cancellationToken);
            throw;
        }

        var ultimaOrdem = await _contexto.Banners.MaxAsync(b => (int?)b.Ordem, cancellationToken) ?? 0;

        var banner = new Banner
        {
            CaminhoRelativoDesktop = desktop.CaminhoRelativo,
            CaminhoRelativoMobile = mobile.CaminhoRelativo,
            NomeOriginalDesktop = desktop.NomeOriginal,
            NomeOriginalMobile = mobile.NomeOriginal,
            TextoAlternativo = string.IsNullOrWhiteSpace(textoAlternativo) ? null : textoAlternativo.Trim(),
            Ordem = ultimaOrdem + 1,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };

        _contexto.Banners.Add(banner);

        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Mesma razao do upload parcial: se o banco recusou, os dois arquivos
            // precisam sair de disco, senao a pasta enche de lixo invisivel.
            await ExcluirArquivosAsync(banner.CaminhoRelativoDesktop, banner.CaminhoRelativoMobile, cancellationToken);
            throw;
        }

        return ParaAdministracao(banner);
    }

    public async Task<BannerAdministracaoDto> AtualizarAsync(
        long id,
        Stream? imagemDesktop,
        string? nomeOriginalDesktop,
        Stream? imagemMobile,
        string? nomeOriginalMobile,
        string? textoAlternativo,
        CancellationToken cancellationToken = default)
    {
        var banner = await _contexto.Banners.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Banner não encontrado.");

        // Guarda os caminhos antigos antes de sobrescrever, para apagar os
        // arquivos substituidos depois que o banco confirmar a gravacao.
        var caminhoDesktopAnterior = banner.CaminhoRelativoDesktop;
        var caminhoMobileAnterior = banner.CaminhoRelativoMobile;

        ImagemArmazenada? novaDesktop = null;
        ImagemArmazenada? novaMobile = null;

        try
        {
            if (imagemDesktop is not null)
            {
                novaDesktop = await _armazenamento.ArmazenarEmPastaAsync(
                    imagemDesktop, nomeOriginalDesktop ?? "desktop", PastaDosBanners, cancellationToken);
            }

            if (imagemMobile is not null)
            {
                novaMobile = await _armazenamento.ArmazenarEmPastaAsync(
                    imagemMobile, nomeOriginalMobile ?? "mobile", PastaDosBanners, cancellationToken);
            }
        }
        catch
        {
            await ExcluirArquivosAsync(
                novaDesktop?.CaminhoRelativo,
                novaMobile?.CaminhoRelativo,
                cancellationToken);
            throw;
        }

        if (novaDesktop is not null)
        {
            banner.CaminhoRelativoDesktop = novaDesktop.CaminhoRelativo;
            banner.NomeOriginalDesktop = novaDesktop.NomeOriginal;
        }

        if (novaMobile is not null)
        {
            banner.CaminhoRelativoMobile = novaMobile.CaminhoRelativo;
            banner.NomeOriginalMobile = novaMobile.NomeOriginal;
        }

        if (textoAlternativo is not null)
        {
            banner.TextoAlternativo = string.IsNullOrWhiteSpace(textoAlternativo) ? null : textoAlternativo.Trim();
        }

        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await ExcluirArquivosAsync(novaDesktop?.CaminhoRelativo, novaMobile?.CaminhoRelativo, cancellationToken);
            throw;
        }

        // A troca so vale depois que o banco aceitou. Se o SaveChanges tivesse
        // falhado, apagar os arquivos antigos deixaria o banner sem imagem nenhuma.
        if (novaDesktop is not null)
        {
            await ExcluirArquivosAsync(caminhoDesktopAnterior, null, cancellationToken);
        }

        if (novaMobile is not null)
        {
            await ExcluirArquivosAsync(caminhoMobileAnterior, null, cancellationToken);
        }

        return ParaAdministracao(banner);
    }

    public async Task<BannerAdministracaoDto> DefinirAtivoAsync(
        long id,
        bool ativo,
        CancellationToken cancellationToken = default)
    {
        var banner = await _contexto.Banners.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Banner não encontrado.");

        banner.Ativo = ativo;

        await _contexto.SaveChangesAsync(cancellationToken);

        return ParaAdministracao(banner);
    }

    public async Task ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        var banner = await _contexto.Banners.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Banner não encontrado.");

        _contexto.Banners.Remove(banner);
        await _contexto.SaveChangesAsync(cancellationToken);

        // Depois do banco, nunca antes: se a gravacao falhar, o banner continua
        // valendo e o arquivo precisa continuar no lugar.
        await ExcluirArquivosAsync(banner.CaminhoRelativoDesktop, banner.CaminhoRelativoMobile, cancellationToken);
    }

    public async Task<IReadOnlyList<BannerAdministracaoDto>> ReordenarAsync(
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        var banners = await _contexto.Banners.ToListAsync(cancellationToken);

        if (banners.Count == 0)
        {
            throw new KeyNotFoundException("Não há banners para reordenar.");
        }

        var existentes = banners.Select(b => b.Id).ToHashSet();
        var recebidos = ids.Distinct().ToList();

        if (recebidos.Count != banners.Count || !existentes.SetEquals(recebidos))
        {
            throw new InvalidOperationException("A ordem informada não corresponde aos banners cadastrados.");
        }

        var porId = banners.ToDictionary(b => b.Id);

        for (var posicao = 0; posicao < recebidos.Count; posicao++)
        {
            porId[recebidos[posicao]].Ordem = posicao;
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        return await ObterTodosAsync(cancellationToken);
    }

    private static BannerAdministracaoDto ParaAdministracao(Banner banner) => new(
        banner.Id,
        banner.CaminhoRelativoDesktop,
        banner.CaminhoRelativoMobile,
        banner.NomeOriginalDesktop,
        banner.NomeOriginalMobile,
        banner.TextoAlternativo,
        banner.Ordem,
        banner.Ativo,
        banner.CriadoEm);

    /// <summary>
    /// Apaga um ou dois arquivos, cada um de forma independente. Aceita nulo
    /// porque quase toda chamada tem só um dos dois lados.
    ///
    /// Este método não engole exceção: a garantia de que arquivo já sumido não
    /// derruba a requisição vem do contrato de <see cref="IArmazenamentoDeImagens.ExcluirAsync"/>,
    /// que trata caminho ausente e caminho invalido como sucesso. Manter isso
    /// aqui valendo evita que a exclusão no banco apareça como falha no painel
    /// por causa de um arquivo que só ocupa espaço em disco.
    /// </summary>
    private async Task ExcluirArquivosAsync(
        string? caminhoDesktop,
        string? caminhoMobile,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(caminhoDesktop))
        {
            await _armazenamento.ExcluirAsync(caminhoDesktop, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(caminhoMobile))
        {
            await _armazenamento.ExcluirAsync(caminhoMobile, cancellationToken);
        }
    }
}
