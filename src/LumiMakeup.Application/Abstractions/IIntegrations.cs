namespace LumiMakeup.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public interface IRecaptchaValidator
{
    Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
}

public interface ICloudinaryService
{
    Task<string> UploadAsync(Stream file, string fileName, string folder, CancellationToken cancellationToken = default);
    Task DeleteAsync(string publicId, CancellationToken cancellationToken = default);
}

public interface IWhatsAppService
{
    Task<bool> SendMessageAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

public interface IFocusNfeService
{
    Task<string> EmitInvoiceAsync(long orderId, CancellationToken cancellationToken = default);
}

public interface IViaCepService
{
    Task<ViaCepResult?> LookupAsync(string cep, CancellationToken cancellationToken = default);
}

public interface INominatimService
{
    Task<(decimal Latitude, decimal Longitude)?> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}

public sealed record ViaCepResult(
    string Cep,
    string Street,
    string Neighborhood,
    string City,
    string State);