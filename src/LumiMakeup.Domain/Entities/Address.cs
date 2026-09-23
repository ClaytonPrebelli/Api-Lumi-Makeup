namespace LumiMakeup.Domain.Entities;

public class Address
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Cep { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsDefault { get; set; }

    public User User { get; set; } = null!;
}