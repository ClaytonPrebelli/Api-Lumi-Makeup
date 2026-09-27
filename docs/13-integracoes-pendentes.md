# 13 — Integrações Pendentes

**Status:** ⬜ não iniciado

---

## Objetivo

Mapear as integrações externas que a API já referencia mas ainda não implementa, para
que ninguém trate stub como funcionalidade pronta.

---

## Stubs registrados

```csharp
services.AddScoped<ICloudinaryService, CloudinaryServiceStub>();
services.AddScoped<IWhatsAppService, WhatsAppServiceStub>();
services.AddScoped<IFocusNfeService, FocusNfeServiceStub>();
```

As interfaces já estão em `Application/Abstractions/IIntegrations.cs` e os registros já
existem em `DependencyInjection.cs`. Só a implementação real falta.

Um stub responde e registra a chamada em log, sem falar com o serviço externo. Isso
mantém a API compilando e o fluxo executável, mas **nada chega ao destino real**.

> Atenção ao `CloudinaryServiceStub`: ele **devolve uma URL de placeholder** em vez de
> falhar. O código que chama o upload recebe uma string válida e segue como se tivesse
> sido feito. Só o `LogWarning` denuncia. É o stub mais enganoso do projeto.

| Integração | Interface | Usada hoje por | Bloqueia |
|---|---|---|---|
| Cloudinary | `ICloudinaryService` | nada | CRUD de produtos |
| WhatsApp (Baileys) | `IWhatsAppService` | nada | avisos de pedido |
| Focus NFe | `IFocusNfeService` | nada | emissão de nota fiscal |
| Brevo | — | **nada** | não integrado (ver `02`) |

---

## Cloudinary — imagens de produto

É a próxima entrega e a que trava a vitrine. Hoje `ImagemProduto` existe no modelo e as
URLs vêm do banco, mas não há como cadastrar uma imagem.

Falta implementar:

1. **Upload assinado.** O `CloudinaryService` precisa gerar uma assinatura
   (`timestamp` + `api_secret` em SHA-1) para que cada upload seja autorizado.
2. **Endpoint de upload** que devolva a URL pública e salve `ImagemProduto` com a
   `Ordem` correspondente.
3. **Endpoint de exclusão** que remova o asset do Cloudinary e o registro.
4. **CRUD de produtos** — criar, editar, ativar/desativar, com as imagens.
5. **Tela de administração** no frontend.

As chaves já têm lugar reservado em `ExternalServices:Cloudinary`
(`NomeNuvem`, `ChaveApi`, `SegredoApi`).

> A assinatura é calculada no servidor justamente para que `api_secret` não vá para o
> navegador. O mesmo cuidado do reCAPTCHA: qualquer segredo que assine requisição é
> responsabilidade do backend.

---

## WhatsApp — avisos de pedido

`RegistroWhatsApp` já está no modelo e ligado a `Pedido` por `Cascade`, com
`StatusRegistroWhatsApp` para acompanhar o envio.

Falta o cliente do serviço Baileys, configurado em `ExternalServices:Baileys`
(`UrlBase` apontando para `http://localhost:3001` e `SegredoCompartilhado`). A ideia é
que um serviço Node separado mantenha a sessão do WhatsApp e a API apenas converse com
ele por HTTP.

| Precisa ser definido | Motivo |
|---|---|
| Quais eventos disparam mensagem | Pagamento aprovado, pedido enviado, entregue |
| Texto e template de cada aviso | |
| Política de retry e rate limit | Vários pedidos podem sair juntos |

---

## Focus NFe — nota fiscal

`NotaFiscal` já existe, com `StatusNotaFiscal`, `ReferenciaFocusNfe`, `UrlXml` e
`UrlPdf`. É uma integração de NF-e, com obrigações fiscais e de conformidade.

Falta o cliente da API da Focus, o mapeamento dos dados fiscais do emitente, e a
emissão disparada por evento de pedido. `StatusNotaFiscal.Pendente` já é o padrão da
entidade, então o fluxo foi pensado desde o início.

> Por envolver responsabilidade fiscal, essa integração deve ser fechada com
> contador antes de ir para produção.

---

## Ordem sugerida

1. **Cloudinary** — sem ela a vitrine fica vazia e o CRUD de produtos não tem sentido.
2. **Checkout e pedidos** — modelo de endereço já pronto (ver `05`); é o que gera
   receita.
3. **WhatsApp** — curto, e melhora a percepção do cliente sobre o pedido.
4. **Focus NFe** — por último, por envolver conformidade.

---

## Como verificar o que é stub

Nomes terminados em `Stub` são implementações fictícias:

```
CloudinaryServiceStub.cs
EmailSenderStub.cs
FocusNfeServiceStub.cs
WhatsAppServiceStub.cs
```

A exceção é `EmailSenderStub`: ele é um **fallback real**, escolhido por `DependencyInjection`
quando o SMTP não está configurado (ver [`11-envio-de-email-smtp.md`](11-envio-de-email-smtp.md)).
