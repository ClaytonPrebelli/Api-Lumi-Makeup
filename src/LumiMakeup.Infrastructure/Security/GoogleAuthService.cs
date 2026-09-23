using Google.Apis.Auth;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Security;

public sealed class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
}

public sealed class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleAuthOptions _options;

    public GoogleAuthService(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public async Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("Google ClientId não configurado (ExternalServices:Google:ClientId).");
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _options.ClientId }
        };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.Name ?? string.Empty);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}