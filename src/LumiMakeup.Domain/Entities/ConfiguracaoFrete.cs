namespace LumiMakeup.Domain.Entities;

public class ConfiguracaoFrete
{
    public long Id { get; set; }
    public string CepOrigem { get; set; } = string.Empty;
    public decimal LatitudeOrigem { get; set; }
    public decimal LongitudeOrigem { get; set; }
    public decimal PrecoPorKm { get; set; }

    /// <summary>
    /// Valor fixo para entrega até 8 km de distância (já com o fator de rota).
    /// </summary>
    public decimal ValorAte8Km { get; set; }

    /// <summary>Valor fixo para entrega de 8 a 16 km.</summary>
    public decimal ValorAte16Km { get; set; }

    /// <summary>Valor fixo para entrega de 16 a 25 km.</summary>
    public decimal ValorAte25Km { get; set; }
}