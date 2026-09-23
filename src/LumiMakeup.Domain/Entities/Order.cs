using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class Order
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ShippingAddressId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.AguardandoPagamento;
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.NaoEnviado;
    public PaymentMethod? PaymentMethod { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public User User { get; set; } = null!;
    public Address ShippingAddress { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<WhatsAppLog> WhatsAppLogs { get; set; } = new List<WhatsAppLog>();
}