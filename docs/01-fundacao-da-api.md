# 01 — Fundação da API

**Status:** ✅ concluído

---

## Objetivo

Montar o esqueleto da API: camadas, injeção de dependências, health check, Swagger,
CORS e o bloco de autenticação por JWT.

---

## As quatro camadas

| Projeto | Responsabilidade | Pode depender de |
|---|---|---|
| `LumiMakeup.Domain` | Entidades e enums | nada |
| `LumiMakeup.Application` | Interfaces e DTOs | `Domain` |
| `LumiMakeup.Infrastructure` | EF Core, serviços, integrações | `Application`, `Domain` |
| `LumiMakeup.Api` | Controllers e composition root | `Infrastructure` |

O domínio é a base e não depende de ninguém. Isso mantém as entidades como POCOs
puras — sem attributes de ORM, sem dependência de biblioteca de terceiros.

### Onde cada coisa mora

- **Entidades** → `LumiMakeup.Domain/Entities/`
- **Enums** → `LumiMakeup.Domain/Enums/`
- **Interfaces de serviço** → `LumiMakeup.Application/Abstractions/`
- **DTOs** → `LumiMakeup.Application/DTOs/`
- **Implementações de serviço** → `LumiMakeup.Infrastructure/Services/`
- **Segurança** → `LumiMakeup.Infrastructure/Security/`
- **Integrações externas** → `LumiMakeup.Infrastructure/Integrations/`
- **Persistência** → `LumiMakeup.Infrastructure/Persistence/`
- **Controllers** → `LumiMakeup.Api/Controllers/`

`Application` define a interface e `Infrastructure` implementa. A interface fica
na camada de dentro, então o `Api` — que é a camada de fora — enxerga apenas
contratos. É por isso que trocar SMTP por outro provedor não toca nos controllers.

---

## `Program.cs` — composition root

Todo o registro de serviços acontece em um único lugar. `AddInfrastructure`
(extension method em `LumiMakeup.Infrastructure/DependencyInjection.cs`) concentra
os registros de banco, integrações e serviços de aplicação.

### Enums serializados como texto

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

Sem isso, `StatusPedido.AguardandoPagamento` viajaria como `1` e o frontend
precisaria conhecer a ordem dos enums. Com o conversor, viaja como
`"AguardandoPagamento"`, legível e estável.

### Connection string

`ConexaoPadrao` é obrigatória — `AddInfrastructure` lança
`InvalidOperationException` se ela não estiver configurada. A aplicação falha na
partida em vez de dar erro em tempo de execução na primeira query.

### Servidor detectando a versão

`ServerVersion.AutoDetect` abre uma conexão na inicialização para descobrir a
versão do MySQL. Como a API não executa migrations no startup, essa detecção é o
primeiro contato com o banco — se a connection string estiver errada, o erro
aparece já no boot.

---

## Health check

```http
GET /api/saude
```

```json
{ "status": "ok", "timestamp": "2026-09-27T20:58:12Z" }
```

Rota pública, sem `[Authorize]`. Usada para verificar se a API está no ar e para
deploy sem tráfego. O timestamp vem de `DateTime.UtcNow`.

---

## Swagger

Habilitado apenas em desenvolvimento:

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

O documento declara o esquema de segurança Bearer para que o botão **Authorize**
funcione no Swagger UI, permitindo testar rotas protegidas colando o access token.

```csharp
public partial class Program { }
```

Essa classe parcial no final de `Program.cs` existe para permitir que os testes
de integração usem `WebApplicationFactory<Program>`.

---

## CORS

A política `LumiCors` é montada a partir de `Cors:OrigensPermitidas`:

```csharp
options.AddPolicy("LumiCors", policy =>
    policy.WithOrigins(origensPermitidas)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
```

Origens vem de `appsettings.json`, nunca hardcoded:

```json
"Cors": { "OrigensPermitidas": [ "http://localhost:4200", "https://lumimakeup.com.br" ] }
```

O frontend roda em `http://localhost:4200`; sem essa origem na lista o navegador
bloqueia toda chamada da API.

> `AllowAnyOrigin` é incompatível com `AllowCredentials`. Por isso a lista é
> explícita.

---

## Bloqueio condicional do JWT

A autenticação só é registrada se `Jwt:Segredo` estiver preenchido:

```csharp
if (opcoesJwt is not null && !string.IsNullOrWhiteSpace(opcoesJwt.Segredo))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)...
    builder.Services.AddAuthorization(...);
}
```

Isso permite rodar e testar a API sem segredos de JWT no arquivo de configuração.
**Sem `Jwt:Segredo`, nenhuma rota `[Authorize]` é protegida** — a API sobe, mas sem
controle de acesso. Em produção o segredo é obrigatório.

A política `SomenteAdministrador` (`RequireRole("Administrador")`) é criada no mesmo
bloco, para uso futuro na área administrativa.

---

## Ordem do pipeline

```csharp
app.UseCors("LumiCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

`UseCors` vem **antes** de autenticação e autorização de propósito: o middleware de
CORS precisa responder aos preflight (`OPTIONS`) antes que qualquer outro middleware
interrompa a requisição.

---

## Seed do administrador

`Program.cs` chama `SemearAdministradorSeConfiguradoAsync` antes de `app.Run()`.
Se `Autenticacao:SeedAdministrador:Email` e `:Senha` estiverem preenchidos, o
`DatabaseSeeder` cria o administrador; caso contrário, apenas registra um aviso.
