namespace LumiMakeup.Domain.Entities;

public class ShippingConfig
{
    public long Id { get; set; }
    public string OriginCep { get; set; } = string.Empty;
    public decimal OriginLatitude { get; set; }
    public decimal OriginLongitude { get; set; }
    public decimal PricePerKm { get; set; }
    public decimal MinimumFee { get; set; }
}