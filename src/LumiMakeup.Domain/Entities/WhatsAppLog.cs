using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class WhatsAppLog
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public WhatsAppLogStatus Status { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Order Order { get; set; } = null!;
}