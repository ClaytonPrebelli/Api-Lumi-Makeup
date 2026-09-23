using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly LumiDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IRecaptchaValidator _recaptchaValidator;
    private readonly IViaCepService _viaCepService;
    private readonly INominatimService _nominatimService;

    public AuthService(
        LumiDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        ITokenService tokenService,
        IGoogleAuthService googleAuthService,
        IRecaptchaValidator recaptchaValidator,
        IViaCepService viaCepService,
        INominatimService nominatimService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _googleAuthService = googleAuthService;
        _recaptchaValidator = recaptchaValidator;
        _viaCepService = viaCepService;
        _nominatimService = nominatimService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var recaptchaValid = await _recaptchaValidator.ValidateTokenAsync(request.RecaptchaToken ?? string.Empty, cancellationToken);
        if (!recaptchaValid)
        {
            throw new InvalidOperationException("Falha na validação do reCAPTCHA.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("E-mail já cadastrado.");
        }

        if (request.Password.Length < 6)
        {
            throw new InvalidOperationException("A senha deve ter no mínimo 6 caracteres.");
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            Role = UserRole.Customer,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new UnauthorizedAccessException("E-mail ou senha inválidos.");
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("E-mail ou senha inválidos.");
        }

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default)
    {
        var info = await _googleAuthService.ValidateIdTokenAsync(request.IdToken, cancellationToken)
            ?? throw new UnauthorizedAccessException("Token do Google inválido.");

        var email = info.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.GoogleId == info.GoogleId || u.Email == email, cancellationToken);

        if (user is null)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
            {
                throw new InvalidOperationException("E-mail já cadastrado com senha. Faça login com e-mail e senha.");
            }

            user = new User
            {
                Name = string.IsNullOrWhiteSpace(info.Name) ? email : info.Name,
                Email = email,
                GoogleId = info.GoogleId,
                Role = UserRole.Customer,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (string.IsNullOrEmpty(user.GoogleId))
        {
            // Vincula a conta Google ao usuário existente (mesmo e-mail).
            user.GoogleId = info.GoogleId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var userId = _tokenService.GetUserIdFromRefreshToken(refreshToken);
        if (userId is null || !long.TryParse(userId, out var id))
        {
            throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");
        }

        var user = await _dbContext.Users.FindAsync([id], cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

        return BuildAuthResponse(user);
    }

    public async Task<UserDto> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Addresses)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

        return ToUserDto(user);
    }

    public async Task<UserDto> CompleteProfileAsync(long userId, CompleteProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.Addresses)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

        var cpf = new string(request.Cpf.Where(char.IsDigit).ToArray());
        if (cpf.Length != 11)
        {
            throw new InvalidOperationException("CPF inválido.");
        }

        var cpfTaken = await _dbContext.Users.AnyAsync(u => u.Cpf == cpf && u.Id != userId, cancellationToken);
        if (cpfTaken)
        {
            throw new InvalidOperationException("CPF já cadastrado.");
        }

        user.Name = request.Name.Trim();
        user.Cpf = cpf;
        user.Phone = request.Phone?.Trim();

        if (request.Address is not null)
        {
            foreach (var oldDefault in user.Addresses.Where(a => a.IsDefault).ToList())
            {
                user.Addresses.Remove(oldDefault);
            }

            var viaCep = await _viaCepService.LookupAsync(request.Address.Cep, cancellationToken);
            var address = new Address
            {
                UserId = user.Id,
                Cep = request.Address.Cep.Trim(),
                Number = request.Address.Number.Trim(),
                Complement = request.Address.Complement?.Trim(),
                IsDefault = true,
                Street = viaCep?.Street ?? string.Empty,
                Neighborhood = viaCep?.Neighborhood ?? string.Empty,
                City = viaCep?.City ?? string.Empty,
                State = viaCep?.State ?? string.Empty
            };

            var fullAddress = $"{address.Street}, {address.Number}, {address.Neighborhood}, {address.City} - {address.State}";
            var geo = await _nominatimService.GeocodeAsync(fullAddress, cancellationToken);
            if (geo.HasValue)
            {
                address.Latitude = geo.Value.Latitude;
                address.Longitude = geo.Value.Longitude;
            }

            user.Addresses.Add(address);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToUserDto(user);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var (accessToken, refreshToken) = _tokenService.GenerateTokens(user);
        return new AuthResponse(accessToken, refreshToken, ToUserDto(user));
    }

    private static UserDto ToUserDto(User user)
    {
        var needsProfile = string.IsNullOrEmpty(user.Cpf);
        return new UserDto(
            user.Id,
            user.Name,
            user.Email,
            user.Cpf,
            user.Phone,
            user.Role,
            needsProfile,
            user.Addresses?.Any(a => a.IsDefault) ?? false);
    }
}