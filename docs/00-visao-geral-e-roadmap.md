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
LumiMakeup.Tests           296 testes automatizados
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
| 15 | Gestão de produtos, categorias, imagens, preço promocional e destaque | [`15-gestao-de-produtos.md`](15-gestao-de-produtos.md) |
| 13 | Melhoria de texto com IA e armazenamento local de imagens | [`13-integracoes-pendentes.md`](13-integracoes-pendentes.md) |

---

## Fundamentos

| # | Documento | Assunto |
|---|---|---|
| 01 | [`01-fundacao-da-api.md`](01-fundacao-da-api.md) | Camadas, `Program.cs`, health check, Swagger, CORS |
| 02 | [`02-configuracao-e-ambiente.md`](02-configuracao-e-ambiente.md) | `appsettings.json`, seções de configuração, seed |
| 03 | [`03-nomenclatura-e-padronizacao.md`](03-nomenclatura-e-padronizacao.md) | Português no código, enums como string |
| 04 | [`04-banco-de-dados-e-ef-core.md`](04-banco-de-dados-e-ef-core.md) | `LumiDbContext`, configurações, migrations |
| 13 | [`13-integracoes-pendentes.md`](13-integracoes-pendentes.md) | Stubs de WhatsApp e Focus NFe; armazenamento local de imagens |
| 14 | [`14-testes-e-qualidade.md`](14-testes-e-qualidade.md) | Estratégia de testes e cobertura |

---

## Roadmap

### Em andamento

Nada. A etapa de produtos foi fechada dos dois lados — API e painel — e a branch
`feature/produtos` está pronta.

### Próximos

- **Checkout e pedidos.** É o que gera receita, e é o que falta para a loja fechar a
  volta. Client-side do carrinho e criação do pedido na API. O modelo de endereço do
  pedido já está pronto (ver `05`).
- **Controle de estoque.** Hoje `QuantidadeEstoque` é editável à mão. Entrada, saída e
  ajuste são o que transformam o painel em operação.
- **Gestão de endereços.** CRUD da agenda do usuário e reutilização no checkout.
- **Cálculo de frete.** No checkout, usando `ConfiguracaoFrete`, com distância
  geocodificada via Nominatim. No carrinho aparece como "a calcular".
- **Emissão de nota fiscal.** Substituir `FocusNfeServiceStub` pela integração real.
- **WhatsApp.** Substituir `WhatsAppServiceStub` pelo cliente Baileys.
- **E-mail transacional.** Ampliar além do reset de senha (confirmação de pedido).

---

## Estado atual

- **296 testes automatizados, todos passando.**
- Cobertura de **linhas em 97,4%** e de **branches em 88,7%**. O detalhamento do que
  ficou por fora está em [`14-testes-e-qualidade.md`](14-testes-e-qualidade.md) — o
  número **caiu de 100%** com o código de produtos e ainda não foi fechado.
- Migrations aplicadas manualmente — a API **não** migra o banco na inicialização.
  Migrations novas: `20260927232105_ImagemProdutoComCaminhoRelativo` e
  `20260928044006_PrecoPromocionalEDestaque`.
- Os dois servidores rodam com a pasta de imagens em `../imagens`, para que o FTP não
  sobrescreva as fotos ao publicar. Em desenvolvimento a API serve essa mesma pasta em
  `/imagens`; em produção quem serve é `imagens.lumimakeup.com.br`, direto do disco.
