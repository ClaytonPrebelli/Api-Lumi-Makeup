using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using LumiMakeup.Infrastructure;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o token JWT: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var origensPermitidas = builder.Configuration.GetSection("Cors:OrigensPermitidas").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("LumiCors", policy =>
        policy.WithOrigins(origensPermitidas)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var secaoJwt = builder.Configuration.GetSection("Jwt");
var opcoesJwt = secaoJwt.Get<TokenJwtOptions>();

if (opcoesJwt is not null && !string.IsNullOrWhiteSpace(opcoesJwt.Segredo))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = opcoesJwt.Emissor,
                ValidAudience = opcoesJwt.Audiencia,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoesJwt.Segredo)),
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("SomenteAdministrador", policy => policy.RequireRole("Administrador"));
    });
}

var app = builder.Build();

await SemearAdministradorSeConfiguradoAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("LumiCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (builder.Configuration.GetValue<bool>("ExecutarServidor", defaultValue: true))
{
    app.Run();
}

static async Task SemearAdministradorSeConfiguradoAsync(WebApplication app)
{
    using var escopo = app.Services.CreateScope();
    var configuration = escopo.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");

    var email = configuration["Autenticacao:SeedAdministrador:Email"];
    var senha = configuration["Autenticacao:SeedAdministrador:Senha"];

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
    {
        logger.LogWarning("SeedAdministrador não configurado (Autenticacao:SeedAdministrador:Email/Senha) — administrador não criado.");
        return;
    }

    var gerador = escopo.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await gerador.SemearAdministradorAsync(new SeedAdministradorOptions(email, senha));
    logger.LogInformation("SeedAdministrador concluído para {Email}.", email);
}

internal sealed class TokenJwtOptions
{
    public string Segredo { get; set; } = string.Empty;
    public string Emissor { get; set; } = string.Empty;
    public string Audiencia { get; set; } = string.Empty;
    public int MinutosDeExpiracao { get; set; } = 60;
    public int DiasDeExpiracaoDoRefresh { get; set; } = 7;
}

public partial class Program
{
}