using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Application.DTOs;

public sealed record RegisterRequest(string Name, string Email, string Password, string? RecaptchaToken);

public sealed record LoginRequest(string Email, string Password);

public sealed record GoogleLoginRequest(string IdToken);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record CompleteProfileRequest(
    string Name,
    string Cpf,
    string? Phone,
    AddressRequest? Address);

public sealed record AddressRequest(
    string Cep,
    string Number,
    string? Complement);

public sealed record UserDto(
    long Id,
    string Name,
    string Email,
    string? Cpf,
    string? Phone,
    UserRole Role,
    bool NeedsProfile,
    bool HasAddress);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    UserDto User);