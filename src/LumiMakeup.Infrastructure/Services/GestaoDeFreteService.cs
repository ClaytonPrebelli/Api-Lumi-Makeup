using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeFreteService : IGestaoDeFreteService
{
    /// <summary>
    /// Quantos CEPs de exemplo a tela oferece. Três não é arbitrário: perto, médio
    /// e longe, que é a faixa em que a fórmula faz sentido.
    /// </summary>
    private static readonly (string Cep, string Rotulo)[] CepsDeExemplo =
    {
        ("01310-300", "mesmo bairro"),
        ("01425-001", "alguns bairros de distância"),
        ("12247-000", "outro cidade")
    };

    private readonly LumiDbContext _contexto;
    private readonly IViaCepService _viaCep;
    private readonly INominatimService _nominatim;
    private readonly ICalculoDeFreteService _calculo;

    public GestaoDeFreteService(
        LumiDbContext contexto,
        IViaCepService viaCep,
        INominatimService nominatim,
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
            // Lista vazia em vez de erro: ainda não configurado é o estado inicial
            // normal de uma loja que está abrindo, e a tela sabe mostrar o
            // formulário de criação para esse caso.
            return new ConfiguracaoDeFreteDto(0, string.Empty, 0m, 0m, null, null);
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
            throw new InvalidOperationException("Informe um CEP válido da loja.");
        }

        if (requisicao.PrecoPorKm <= 0)
        {
            throw new InvalidOperationException("Informe quanto custa cada quilômetro.");
        }

        if (requisicao.TaxaMinima < 0)
        {
            throw new InvalidOperationException("A taxa mínima não pode ser negativa.");
        }

        // A coordenada vem do CEP, e não é digitada. Pedir lat/long na tela faria a
        // administradora colar um número de mapa no lugar errado, e o frete sairia
        // errado sem nenhuma pista.
        var consultado = await _viaCep.ConsultarAsync(cep, cancellationToken);

        if (consultado is null)
        {
            throw new InvalidOperationException("Não encontramos esse CEP. Confira os números.");
        }

        var origem = $"\"{cep}\", {consultado.Logradouro}, {consultado.Bairro}, {consultado.Cidade}, {consultado.Estado}";
        var coordenadas = await _nominatim.GeocodificarAsync(origem, cancellationToken);

        if (coordenadas is null)
        {
            throw new InvalidOperationException(
                "Localizamos o CEP, mas não as coordenadas. Tente de novo em instantes.");
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
        existente.TaxaMinima = requisicao.TaxaMinima;

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
            throw new InvalidOperationException("Informe um CEP válido para simular.");
        }

        return _calculo.CalcularAsync(
            new EnderecoDeEntregaRequisicao(limpo, string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty),
            cancellationToken);
    }

    /// <summary>
    /// Calcula o frete para três CEPs de exemplo e devolve junto da configuração.
    ///
    /// A tela mostra isso porque "R$ 1,20 por km" não diz nada concreto: só
    /// "para o CEP 01425 sai R$ 12,40" faz a administradora saber se a tarifa
    /// está boa. E a simulação usa o mesmo cálculo do checkout, então o número
    /// da tela é o número que o cliente vai ver.
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
            configuracao.TaxaMinima,
            exemplo?.DistanciaKm,
            exemplo?.Custo);
    }

    /// <summary>
    /// Frete para o CEP de exemplo mais próximo da loja que o ViaCEP reconhece.
    ///
    /// A tela mostra o exemplo porque "R$ 1,20 por km" não diz nada concreto: só
    /// "para o CEP 01425 sai R$ 12,40" faz a administradora saber se a tarifa
    /// está boa. E a conta é a mesma do checkout, então o número da tela é o
    /// número que o cliente vai ver.
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
                    new EnderecoDeEntregaRequisicao(cep, "1", "1", null, "Centro", "São Paulo", "SP"),
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // O exemplo é ilustrativo. Se este CEP não geocodificar, a tela
                // mostra a configuração sem exemplo, em vez de impedir a edição de
                // uma tarifa que pode estar correta.
                return null;
            }
        }

        return null;
    }
}
