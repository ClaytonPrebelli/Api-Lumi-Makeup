using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class CalculoDeFreteService : ICalculoDeFreteService
{
    /// <summary>
    /// Fator de 1,3 sobre a distância em linha reta.
    ///
    ///km em linha reta não é o caminho percorrido: rua é uma linha só e a
    /// distância real é maior. A razão fica em torno de 1,3 para rua urbana. É
    /// uma aproximação, e está registrada como tal: a tarifa final depende de
    /// quem contracta o transporte, e este número é o ponto a ajustar.
    /// </summary>
    private const decimal FatorDeRotaUrbana = 1.3m;

    /// <summary>
    /// Acima disso o valor de transporte não é confiável, porque a distância
    /// vira estimativa grosseira. Recusar é melhor que cobrar um frete errado a
    /// alguém que mora longe.
    /// </summary>
    private const decimal DistanciaMaximaEmKm = 900m;

    private readonly LumiDbContext _contexto;
    private readonly IGeocodificador _geocodificador;

    public CalculoDeFreteService(LumiDbContext contexto, IGeocodificador geocodificador)
    {
        _contexto = contexto;
        _geocodificador = geocodificador;
    }

    public async Task<CalculoDeFreteDto> CalcularAsync(
        EnderecoDeEntregaRequisicao destino,
        CancellationToken cancellationToken = default)
    {
        var configuracao = await _contexto.ConfiguracoesDeFrete
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (configuracao is null)
        {
            // Sem tabela preenchida não existe tarifa, e inventar uma daria um
            // número no total que ninguém explaining depois.
            throw new InvalidOperationException(
                "O frete ainda não foi configurado para a loja. Fale com a administradora.");
        }

        var consulta = MontarConsultaDeGeocodificacao(destino);

        var coordenadas = await _geocodificador.GeocodificarAsync(consulta, cancellationToken);

        if (coordenadas is null)
        {
            // O CEP foi válido, mas o serviço externo não achou o lugar. Sem as
            // coordenadas não há distância, e sem distância não há frete honesto.
            throw new InvalidOperationException(
                "Não conseguimos localizar esse endereço para calcular o frete. Confira o CEP e tente de novo.");
        }

        var (latitude, longitude) = coordenadas.Value;

        var distancia = DistanciaEmKm(
            configuracao.LatitudeOrigem,
            configuracao.LongitudeOrigem,
            latitude,
            longitude) * FatorDeRotaUrbana;

        // A distância já vem com o fator de rota urbana acima, e é esta que
        // volta no DTO. A tela mostra o mesmo número que o cálculo usou, para o
        // cliente não ver "13,2 km" e pagar por outra coisa.
        distancia = Math.Round(distancia, 2, MidpointRounding.AwayFromZero);

        if (distancia > DistanciaMaximaEmKm)
        {
            throw new InvalidOperationException(
                "Esse endereço está longe demais para envio pelo Correios. Fale com a loja para combinar.");
        }

        var custo = Math.Round(distancia * configuracao.PrecoPorKm, 2, MidpointRounding.AwayFromZero);

        // A taxa mínima é o piso do frete. Sem ela, um pedido de 300 metros
        // custaria quase nada para a loja empacotar e enviar.
        var frete = Math.Max(custo, configuracao.TaxaMinima);
        frete = Math.Round(frete, 2, MidpointRounding.AwayFromZero);

        return new CalculoDeFreteDto(
            distancia,
            frete,
            configuracao.PrecoPorKm,
            configuracao.TaxaMinima);
    }

    /// <summary>
    /// Monta o endereço para a geocodificação, sem campos vazios.
    ///
    /// A versão anterior interpolava todos os campos do endereço, mesmo vazios.
    /// Na simulação da tela de frete o CEP vem sozinho, e a consulta virava
    /// "18080001", , , , , — que o geocodificador não encontrava, e o cálculo
    /// recusava com "não conseguimos localizar esse endereço". O checkout não
    /// sofria com isso porque o CEP é consultado antes e preenche o resto.
    ///
    /// Descartar os vazios também deixa a consulta mais curta e mais fácil de
    /// acertar: cada parte que existe chega inteira para o geocodificador.
    /// </summary>
    internal static string MontarConsultaDeGeocodificacao(EnderecoDeEntregaRequisicao destino)
    {
        var partes = new[] { destino.Cep, destino.Logradouro, destino.Numero, destino.Bairro, destino.Cidade, destino.Estado }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim());

        return string.Join(", ", partes);
    }

    /// <summary>
    /// Distância em linha reta entre dois pontos, pela fórmula de Haversine.
    ///
    /// A alternativa simples (Pitágoras em lat/long) trata graus como se fossem
    /// metros, e o erro passa de 1% logo acima de 1 grau de latitude. Dentro de
    /// uma cidade a diferença é pequena, mas entre estados ela é grande demais
    /// para entrar no total.
    /// </summary>
    public static decimal DistanciaEmKm(
        decimal latitude1,
        decimal longitude1,
        decimal latitude2,
        decimal longitude2)
    {
        const double raioDaTerraEmKm = 6371.0;

        var lat1 = (double)latitude1 * Math.PI / 180.0;
        var lat2 = (double)latitude2 * Math.PI / 180.0;
        var diferencaDeLat = (double)(latitude2 - latitude1) * Math.PI / 180.0;
        var diferencaDeLon = (double)(longitude2 - longitude1) * Math.PI / 180.0;

        var a = Math.Sin(diferencaDeLat / 2) * Math.Sin(diferencaDeLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2)
                * Math.Sin(diferencaDeLon / 2) * Math.Sin(diferencaDeLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return (decimal)(raioDaTerraEmKm * c);
    }
}
