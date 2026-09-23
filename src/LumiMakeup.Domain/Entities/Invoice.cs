using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class Invoice
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pendente;
    public string? FocusNfeReference { get; set; }
    public string? XmlUrl { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime? IssuedAt { get; set; }

    public Order Order { get; set; } = null!;
}