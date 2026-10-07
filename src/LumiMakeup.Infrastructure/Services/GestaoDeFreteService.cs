using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeFreteService : IGestaoDeFreteService
{
    /// <summary>
    /// Quantos CEPs de exemplo a tela oferece. TrÃªs nÃ£o Ã© arbitrÃ¡rio: perto, mÃ©dio
    /// e longe, que Ã© a faixa em que a fÃ³rmula faz sentido.
    /// </summary>
    private static readonly (string Cep, string Rotulo)[] CepsDeExemplo =
    {
        ("01310-300", "mesmo bairro"),
        ("01425-001", "alguns bairros de distÃ¢ncia"),
        ("12247-000", "outro cidade")
    };

    private readonly LumiDbContext _contexto;
    private readonly IViaCepService _viaCep;
    private readonly IGeocodificador _nominatim;
    private readonly ICalculoDeFreteService _calculo;

    public GestaoDeFreteService(
        LumiDbContext contexto,
        IViaCepService viaCep,
        IGeocodificador nominatim,
        ICalculoDeFreteService calculo)
    {
        _contexto = contexto;
        _viaCep = viaCep;
        _nominatim = nominatim;
        _calculo = calculo;
    }

    public async Task<ConfiguracaoDeFreteDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var configuracao = await _contexto.ConfiguracoesDeFrete
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (configuracao is null)
        {
            // Lista vazia em vez de erro: ainda nÃ£o configurado Ã© o estado inicial
            // normal de uma loja que estÃ¡ abrindo, e a tela sabe mostrar o
            // formulÃ¡rio de criaÃ§Ã£o para esse caso.
            return new ConfiguracaoDeFreteDto(0, string.Empty, 0m, 0m, 0m, 0m, null, null);
        }

        return await ComExemploAsync(configuracao, cancellationToken);
    }

    public async Task<ConfiguracaoDeFreteDto> SalvarAsync(
        RequisicaoDeConfiguracaoDeFrete requisicao,
        CancellationToken cancellationToken = default)
    {
        var cep = new string(requisicao.CepOrigem?.Where(char.IsDigit).ToArray() ?? []);

        if (cep.Length != 8)
        {
            throw new InvalidOperationException("Informe um CEP vÃ¡lido da loja.");
        }

        if (requisicao.PrecoPorKm <= 0)
        {
            throw new InvalidOperationException("Informe quanto custa cada quilÃ´metro.");
        }

        if (requisicao.ValorAte8Km < 0 || requisicao.ValorAte16Km < 0 || requisicao.ValorAte25Km < 0)
        {
            throw new InvalidOperationException("Os valores das faixas nÃ£o podem ser negativos.");
        }

        // A coordenada vem do CEP, e nÃ£o Ã© digitada. Pedir lat/long na tela faria a
        // administradora colar um nÃºmero de mapa no lugar errado, e o frete sairia
        // errado sem nenhuma pista.
        var consultado = await _viaCep.ConsultarAsync(cep, cancellationToken);

        if (consultado is null)
        {
            throw new InvalidOperationException("NÃ£o encontramos esse CEP. Confira os nÃºmeros.");
        }

        var origem = $"\"{cep}\", {consultado.Logradouro}, {consultado.Bairro}, {consultado.Cidade}, {consultado.Estado}";
        var coordenadas = await _nominatim.GeocodificarAsync(origem, cancellationToken);

        if (coordenadas is null)
        {
            throw new InvalidOperationException(
                "Localizamos o CEP, mas nÃ£o as coordenadas. Tente de novo em instantes.");
        }

        var (latitude, longitude) = coordenadas.Value;

        var existente = await _contexto.ConfiguracoesDeFrete
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existente is null)
        {
            existente = new ConfiguracaoFrete();
            _contexto.ConfiguracoesDeFrete.Add(existente);
        }

        existente.CepOrigem = cep;
        existente.LatitudeOrigem = latitude;
        existente.LongitudeOrigem = longitude;
        existente.PrecoPorKm = requisicao.PrecoPorKm;
        existente.ValorAte8Km = requisicao.ValorAte8Km;
        existente.ValorAte16Km = requisicao.ValorAte16Km;
        existente.ValorAte25Km = requisicao.ValorAte25Km;

        await _contexto.SaveChangesAsync(cancellationToken);

        return await ComExemploAsync(existente, cancellationToken);
    }

    public Task<CalculoDeFreteDto> SimularAsync(
        string cep,
        CancellationToken cancellationToken = default)
    {
        var limpo = new string((cep ?? string.Empty).Where(char.IsDigit).ToArray());

        if (limpo.Length != 8)
        {
            throw new InvalidOperationException("Informe um CEP vÃ¡lido para simular.");
        }

        return _calculo.CalcularAsync(
            new EnderecoDeEntregaRequisicao(limpo, string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty),
            cancellationToken);
    }

    /// <summary>
    /// Calcula o frete para trÃªs CEPs de exemplo e devolve junto da configuraÃ§Ã£o.
    ///
    /// A tela mostra isso porque "R$ 1,20 por km" nÃ£o diz nada concreto: sÃ³
    /// "para o CEP 01425 sai R$ 12,40" faz a administradora saber se a tarifa
    /// estÃ¡ boa. E a simulaÃ§Ã£o usa o mesmo cÃ¡lculo do checkout, entÃ£o o nÃºmero
    /// da tela Ã© o nÃºmero que o cliente vai ver.
    /// </summary>
    private async Task<ConfiguracaoDeFreteDto> ComExemploAsync(
        ConfiguracaoFrete configuracao,
        CancellationToken cancellationToken)
    {
        var exemplo = await CalcularExemploAsync(configuracao, cancellationToken);

        return new ConfiguracaoDeFreteDto(
            configuracao.Id,
            configuracao.CepOrigem,
            configuracao.PrecoPorKm,
            configuracao.ValorAte8Km,
            configuracao.ValorAte16Km,
            configuracao.ValorAte25Km,
            exemplo?.DistanciaKm,
            exemplo?.Custo);
    }

    /// <summary>
    /// Frete para o CEP de exemplo mais prÃ³ximo da loja que o ViaCEP reconhece.
    ///
    /// A tela mostra o exemplo porque "R$ 1,20 por km" nÃ£o diz nada concreto: sÃ³
    /// "para o CEP 01425 sai R$ 12,40" faz a administradora saber se a tarifa
    /// estÃ¡ boa. E a conta Ã© a mesma do checkout, entÃ£o o nÃºmero da tela Ã© o
    /// nÃºmero que o cliente vai ver.
    /// </summary>
    private async Task<CalculoDeFreteDto?> CalcularExemploAsync(
        ConfiguracaoFrete configuracao,
        CancellationToken cancellationToken)
    {
        foreach (var (cepDeExemplo, _) in CepsDeExemplo)
        {
            var cep = new string(cepDeExemplo.Where(char.IsDigit).ToArray());

            if (await _viaCep.ConsultarAsync(cep, cancellationToken) is null)
            {
                continue;
            }

            try
            {
                return await _calculo.CalcularAsync(
                    new EnderecoDeEntregaRequisicao(cep, "1", "1", null, "Centro", "SÃ£o Paulo", "SP"),
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // O exemplo Ã© ilustrativo. Se este CEP nÃ£o geocodificar, a tela
                // mostra a configuraÃ§Ã£o sem exemplo, em vez de impedir a ediÃ§Ã£o de
                // uma tarifa que pode estar correta.
                return null;
            }
        }

        return null;
    }
}
