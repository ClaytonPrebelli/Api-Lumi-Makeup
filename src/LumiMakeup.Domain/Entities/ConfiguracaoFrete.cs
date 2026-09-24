namespace LumiMakeup.Domain.Entities;

public class ConfiguracaoFrete
{
    public long Id { get; set; }
    public string CepOrigem { get; set; } = string.Empty;
    public decimal LatitudeOrigem { get; set; }
    public decimal LongitudeOrigem { get; set; }
    public decimal PrecoPorKm { get; set; }
    public decimal TaxaMinima { get; set; }
}