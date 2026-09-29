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
LumiMakeup.Tests           330 testes automatizados
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
| 17 | Banners do hero (dois formatos por slide, ativação e ordem) | [`17-banners.md`](17-banners.md) |
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

Nada. A etapa de banner foi fechada dos dois lados, e a de produtos antes dela.

### Próximos

A ordem mudou depois de decidir cupom e venda de balcão. **As duas features dependem
de criação de pedido, que não existe** — não há nenhum `new Pedido` na API, e nada
decrementa estoque. Por isso o **núcleo de pedido vem primeiro**, e é o caminho
crítico da loja inteira.

1. **Núcleo de pedido.** Cálculo de subtotal, frete, desconto e total; baixa de
   estoque; mudança de status. Vira o serviço único usado pelo checkout, pelo cupom e
   pela venda de balcão — sem lógica de total duplicada em três lugares.
2. **Checkout e pedidos.** Telas de checkout, confirmação, meus pedidos e detalhe. É o
   que gera receita. O modelo de endereço do pedido já está pronto (ver `05`).
3. **Cupom de desconto.** Ver as decisões abaixo.
4. **Venda de balcão.** Ver as decisões abaixo.
5. **Controle de estoque.** A baixa vem com o núcleo de pedido. Falta entrada, saída
   e ajuste, e o formulário de produto ganhar **somar unidades** em vez de só reescrever
   a quantidade absoluta.
6. **Gestão de endereços.** CRUD da agenda do usuário e reutilização no checkout.
7. **Cálculo de frete.** No checkout, usando `ConfiguracaoFrete`, com distância
   geocodificada via Nominatim. No carrinho aparece como "a calcular".
8. **Emissão de nota fiscal.** Substituir `FocusNfeServiceStub` pela integração real.
9. **WhatsApp.** Substituir `WhatsAppServiceStub` pelo cliente Baileys.
10. **E-mail transacional.** Ampliar além do reset de senha (confirmação de pedido).

---

## Cupom de desconto

**Decidido.** Vai para o checkout, e o desconto incide sobre o **subtotal dos
produtos** — nunca sobre o frete.

| Campo | Regra |
|---|---|
| `Codigo` | O que o cliente digita. Único |
| `Percentual` | Desconto em % |
| `QuantidadeDisponivel` | Pool total; **decrementa a cada uso** |
| `ValorMinimo` | Subtotal mínimo de produtos para o cupom valer |
| `ValidadeAte` | Data de término |
| `Ativo` | Chave liga/desliga |

**Regras de validação**, na ordem em que são checadas:

| Situação | Mensagem |
|---|---|
| Código inexistente | "Cupom inválido." |
| Desativado | "Cupom inválido." |
| Esgotado (`QuantidadeDisponivel == 0`) | "Cupom inválido." |
| Vencido | "Cupom expirado." |
| Subtotal abaixo do mínimo | "Cupom válido para compras acima de R$ X." |

Desativado e esgotado dão a **mesma** mensagem ("Cupom inválido."), e não messages
diferentes: os dois significam, para quem está comprando, que o código não vale.
Divergir só entregaria informação de negócio de graça.

**Quando a quantidade volta:** ao aumentar `QuantidadeDisponivel` no painel. Não há
reposição automática, e "quantidade" é o controle escolhido, não um contador de uso.

**Cálculo:** `(subtotal dos produtos) × percentual`, aplicado **depois** do preço
promocional, e arredondado para centavos. O frete entra no total depois do desconto,
sem ser afetado por ele.

> **O desconto é sobre produtos, e isso é regra de negócio, não detalhe de layout.** Cupom
> sobre frete subsidia o transporte e não a mercadoria, que é a intenção de uma
> promoção. Também muda a base de cálculo: `Total = Subtotal − Desconto + Frete`, e não
> `Total = (Subtotal + Frete) × (1 − p)`.

**A baixa da quantidade e a criação do pedido precisam ser atômicas.** Se a linha do
cupom for decrementada e a do pedido falhar, o cliente perde um cupom que não usou. As
duas gravações vão na mesma transação.

---

## Venda de balcão

**Decidido.** O admin registra uma venda presencial pelo painel, para que ela componha
estoque e relatório como qualquer outro pedido.

| Item | Decisão |
|---|---|
| Cliente | **Avulso permitido.** Venda sem conta, com nome e documento digitados |
| Baixa de estoque | Sim, igual à de qualquer pedido |
| Data | Informada, para registrar venda de dia anterior |
| Pagamento | Pix, cartão, dinheiro ou outro |

**Exige um campo de origem no pedido** (`Online` / `Balcao`). Sem ele a venda de balcão
entra misturada no relatório de receita e não dá para separar o que veio do site.

**Cliente avulso muda o schema:** hoje `Pedido.UsuarioId` é obrigatório e o endereço
também. Uma venda de balcão não tem nem um nem o outro. `UsuarioId` passa a ser
anulável, e nome e documento do cliente guardado no próprio pedido — na linha do
endereço, que já é cópia imutável, e que não deve mudar se o cadastro do cliente
mudar depois.

**Cancelamento devolve o estoque**, e o mesmo vale para venda de balcão.

---

## Estado atual

- **330 testes automatizados, todos passando.**
- Cobertura de **linhas em 97,4%** e de **branches em 88,6%**. O detalhamento do que
  ficou por fora está em [`14-testes-e-qualidade.md`](14-testes-e-qualidade.md) — o
  número **caiu de 100%** com o código de produtos e ainda não foi fechado.
- **Migrations nunca são aplicadas em deploy nem no servidor.** A API não migra o banco
  na inicialização, o workflow não tem passo de banco, e o servidor não recebe schema.
  Aplicação é local, na máquina de desenvolvimento, contra o mesmo banco que serve a
  produção. Ver [`04-banco-de-dados-e-ef-core.md`](04-banco-de-dados-e-ef-core.md).
  Migrations aplicadas: `20260927232105_ImagemProdutoComCaminhoRelativo`,
  `20260928044006_PrecoPromocionalEDestaque` e
  `20260928235512_CriacaoDaTabelaDeBanners`.
- Os dois servidores rodam com a pasta de imagens em `../imagens`, para que o FTP não
  sobrescreva as fotos ao publicar. Em desenvolvimento a API serve essa mesma pasta em
  `/imagens`; em produção quem serve é `imagens.lumimakeup.com.br`, direto do disco.
- **Deploy automático pela branch `main`** — testes, `dotnet publish` e envio por FTP
  para `api.lumimakeup.com.br/`. Publicar com a API no ar exige reciclar o pool de
  aplicações antes, senão as DLLs travadas não são sobrescritas. Ver
  [`16-ci-cd-e-deploy.md`](16-ci-cd-e-deploy.md).
