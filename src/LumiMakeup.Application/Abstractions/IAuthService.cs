using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<UserDto> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserDto> CompleteProfileAsync(long userId, CompleteProfileRequest request, CancellationToken cancellationToken = default);
}