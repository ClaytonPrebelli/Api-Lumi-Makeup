# 00 — Visão Geral e Roadmap

> Documento equivalente do frontend: `LumiMakeup/docs/00-visao-geral-e-roadmap.md`.

---

## O que é este repositório

API da **Lumi Makeup**, loja de cosméticos. Backend em **.NET 8 / ASP.NET Core**, com
**EF Core** sobre **MySQL**, organizado em quatro camadas.

O frontend Angular é um repositório separado (`LumiMakeup`) e consome esta API via HTTP.

---

## Stack

| Item | Escolha |
|---|---|
| Runtime | .NET 8 |
| Framework | ASP.NET Core (Web API) |
| ORM | Entity Framework Core 8 + Pomelo (MySQL) |
| Banco | MariaDB 10.11 (provider do Pomelo; ver doc `04`) |
| Autenticação | JWT (HMAC-SHA256) |
| Hash de senha | `PasswordHasher<Usuario>` (ASP.NET Core Identity) |
| Documentação da API | Swagger / OpenAPI |
| Testes | xUnit + Moq + EF Core InMemory |
| Cobertura | coverlet.msbuild |
| E-mail | SMTP |
| HTTP externo | `HttpClient` tipado (ViaCEP, Nominatim, Google, reCAPTCHA) |

---

## Arquitetura em camadas

```
LumiMakeup.Domain          entidades, enums — sem dependências externas
LumiMakeup.Application     interfaces (abstrações) e DTOs
LumiMakeup.Infrastructure  EF Core, serviços, segurança, integrações
LumiMakeup.Api             controllers, composition root, Swagger, CORS
LumiMakeup.Tests           169 testes automatizados
```

A regra é sempre a mesma: **dependências apontam para dentro.**
`Api` conhece `Infrastructure`, que conhece `Application` e `Domain`. O domínio não
depende de nada — é por isso que as entidades são POCOs simples.

Detalhe em [`01-fundacao-da-api.md`](01-fundacao-da-api.md).

---

## Funcionalidades entregues

| # | Funcionalidade | Documento |
|---|---|---|
| 06 | Autenticação JWT (access + refresh) | [`06-autenticacao-jwt.md`](06-autenticacao-jwt.md) |
| 07 | Cadastro, login e-mail e login social Google | [`07-cadastro-e-login.md`](07-cadastro-e-login.md) |
| 08 | Recuperação e redefinição de senha | [`08-recuperacao-de-senha.md`](08-recuperacao-de-senha.md) |
| 09 | Consulta de CEP | [`09-consulta-de-cep.md`](09-consulta-de-cep.md) |
| 10 | Validação de reCAPTCHA no cadastro | [`10-validacao-recaptcha.md`](10-validacao-recaptcha.md) |
| 11 | Envio de e-mail transacional via SMTP | [`11-envio-de-email-smtp.md`](11-envio-de-email-smtp.md) |
| 12 | Catálogo de produtos e categorias (leitura) | [`12-catalogo.md`](12-catalogo.md) |
| 05 | Endereço de entrega próprio e imutável no pedido | [`05-enderecos-e-pedidos.md`](05-enderecos-e-pedidos.md) |

---

## Fundamentos

| # | Documento | Assunto |
|---|---|---|
| 01 | [`01-fundacao-da-api.md`](01-fundacao-da-api.md) | Camadas, `Program.cs`, health check, Swagger, CORS |
| 02 | [`02-configuracao-e-ambiente.md`](02-configuracao-e-ambiente.md) | `appsettings.json`, seções de configuração, seed |
| 03 | [`03-nomenclatura-e-padronizacao.md`](03-nomenclatura-e-padronizacao.md) | Português no código, enums como string |
| 04 | [`04-banco-de-dados-e-ef-core.md`](04-banco-de-dados-e-ef-core.md) | `LumiDbContext`, configurações, migrations |
| 13 | [`13-integracoes-pendentes.md`](13-integracoes-pendentes.md) | Stubs de Cloudinary, WhatsApp, Focus NFe |
| 14 | [`14-testes-e-qualidade.md`](14-testes-e-qualidade.md) | Estratégia de testes e cobertura |

---

## Roadmap

### Em andamento

**CRUD de produtos com imagens (Cloudinary).**
`ICloudinaryService` já existe, mas a implementação é um stub. Falta:
implementação real com upload assinado, endpoints de escrita, edição de produto
e a tela de administração.

### Próximos

- **Checkout.** Client-side do carrinho e criação do pedido na API. O modelo de
  endereço do pedido já está pronto (ver `05`); falta o serviço de pedidos e o
  cálculo de frete.
- **Frete.** Cálculo no checkout usando `ConfiguracaoFrete`, com distância
  geocodificada via Nominatim. No carrinho o frete aparece como "a calcular".
- **Gestão de endereços.** CRUD da agenda do usuário e reutilização no checkout.
- **Emissão de nota fiscal.** Substituir `FocusNfeServiceStub` pela integração real.
- **WhatsApp.** Substituir `WhatsAppServiceStub` pelo cliente Baileys.
- **E-mail transacional.** Ampliar além do reset de senha (confirmação de pedido).

---

## Estado atual

- 169 testes automatizados, todos passando.
- Cobertura de **linhas em 100%**. Cobertura de branches em 93,5% — 8 branches
  parciais, a maioria em caminhos de falha de integração externa.
- Migrations aplicadas manualmente — a API **não** migra o banco na inicialização.
