using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeFreteService : IGestaoDeFreteService
{
    private readonly LumiDbContext _contexto;
    private readonly IViaCepService _viaCep;
    private readonly IGeocodificador _geocodificador;
    private readonly ICalculoDeFreteService _calculo;

    public GestaoDeFreteService(
        LumiDbContext contexto,
        IViaCepService viaCep,
        IGeocodificador geocodificador,
        ICalculoDeFreteService calculo)
    {
        _contexto = contexto;
        _viaCep = viaCep;
        _geocodificador = geocodificador;
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

        // O mesmo caminho do checkout: o geocodificador prefere o ponto do CEP
        // e cai para o centro da cidade quando o OpenStreetMap nao tem o CEP
        // mapeado.
        var consulta = CalculoDeFreteService.MontarConsultaDeGeocodificacao(
            new EnderecoDeEntregaRequisicao(
                $"{consultado.Cep[..5]}-{consultado.Cep[5..]}",
                consultado.Logradouro,
                string.Empty,
                null,
                consultado.Bairro,
                consultado.Cidade,
                consultado.Estado));

        var coordenadas = await _geocodificador.GeocodificarAsync(consulta, cancellationToken);

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

    public async Task<CalculoDeFreteDto> SimularAsync(
        string cep,
        CancellationToken cancellationToken = default)
    {
        var limpo = new string((cep ?? string.Empty).Where(char.IsDigit).ToArray());

        if (limpo.Length != 8)
        {
            throw new InvalidOperationException("Informe um CEP válido para simular.");
        }

        // O CEP sozinho não localiza nada: o geocodificador precisa do endereço
        // inteiro, e é ele que faz a busca dar certo. Consultar o CEP aqui é o
        // que o checkout já faz, e transforma o número em um endereço completo
        // antes de geocodificar.
        var consultado = await _viaCep.ConsultarAsync(limpo, cancellationToken);

        if (consultado is null)
        {
            throw new InvalidOperationException("Não encontramos esse CEP. Confira os números.");
        }

        return await _calculo.CalcularAsync(
            new EnderecoDeEntregaRequisicao(
                $"{limpo[..5]}-{limpo[5..]}",
                consultado.Logradouro,
                string.Empty,
                null,
                consultado.Bairro,
                consultado.Cidade,
                consultado.Estado),
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
        // O exemplo precisa sair de perto da loja. Com CEPs fixos de São Paulo,
        // uma loja de Sorocaba veria um número de cidade errada - e o cartão se
        // chama "perto da loja".
        var origem = await _viaCep.ConsultarAsync(configuracao.CepOrigem, cancellationToken);

        if (origem is null)
        {
            return null;
        }

        // O quinto dígito do CEP avança de bairro em bairro dentro da mesma
        // cidade, e é o jeito mais simples de achar vizinhos da loja sem manter
        // uma lista de bairros que envelhece a cada mudança de CEP.
        foreach (var cep in CepsVizinhos(configuracao.CepOrigem))
        {
            var vizinho = await _viaCep.ConsultarAsync(cep, cancellationToken);

            if (vizinho is null ||
                string.Equals(vizinho.Cidade, origem.Cidade, StringComparison.OrdinalIgnoreCase) is false ||
                string.Equals(vizinho.Estado, origem.Estado, StringComparison.OrdinalIgnoreCase) is false)
            {
                continue;
            }

            try
            {
                return await _calculo.CalcularAsync(
                    new EnderecoDeEntregaRequisicao(
                        $"{vizinho.Cep[..5]}-{vizinho.Cep[5..]}",
                        vizinho.Logradouro,
                        string.Empty,
                        null,
                        vizinho.Bairro,
                        vizinho.Cidade,
                        vizinho.Estado),
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

    /// <summary>
    /// CEPs de 8 dígitos a tentar como vizinho da loja, do mais próximo para o
    /// mais distante.
    ///
    /// Um CEP de Sorocaba é 18072-759: cinco dígitos de faixa e três do
    /// quarteirão. O que muda entre bairros vizinhos é o quinto dígito, então é
    /// por ele que a busca anda. O CEP da própria loja entra primeiro, para o
    /// caso de a loja estar num CEP que cobre só o seu quarteirão.
    ///
    /// O CEP precisa sair com oito dígitos. Montar "1807-000" daria sete, e o
    /// ViaCEP não reconheceria - o exemplo sumiria da tela sem aviso.
    /// </summary>
    private static IEnumerable<string> CepsVizinhos(string cepDaLoja)
    {
        yield return cepDaLoja;

        var faixa = cepDaLoja[..5];
        var quinto = faixa[^1];

        // O quinto dígito sobe e desce, sem dar a volta: 9 desce para 8 e 0 sobe
        // para 1, porque um dígito acima de 9 não existe.
        var acima = quinto == '9' ? '8' : (char)(quinto + 1);
        var abaixo = quinto == '0' ? '1' : (char)(quinto - 1);

        yield return $"{faixa[..4]}{acima}000";
        yield return $"{faixa[..4]}{abaixo}000";
    }
}
