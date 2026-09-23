using LumiMakeup.Domain.Entities;

namespace LumiMakeup.Application.Abstractions;

public interface ITokenService
{
    (string AccessToken, string RefreshToken) GenerateTokens(User user);
    string? GetUserIdFromRefreshToken(string refreshToken);
}

public interface IGoogleAuthService
{
    Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record GoogleUserInfo(string GoogleId, string Email, string Name);