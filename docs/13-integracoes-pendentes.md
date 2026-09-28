# 13 — Integrações Pendentes

**Status:** ⬜ não iniciado

---

## Objetivo

Mapear as integrações externas que a API já referencia mas ainda não implementa, para
que ninguém trate stub como funcionalidade pronta.

---

## Stubs registrados

```csharp
services.AddScoped<IWhatsAppService, WhatsAppServiceStub>();
services.AddScoped<IFocusNfeService, FocusNfeServiceStub>();
```

As interfaces já estão em `Application/Abstractions/IIntegrations.cs` e os registros já
existem em `DependencyInjection.cs`. Só a implementação real falta.

Um stub responde e registra a chamada em log, sem falar com o serviço externo. Isso
mantém a API compilando e o fluxo executável, mas **nada chega ao destino real**.

| Integração | Interface | Usada hoje por | Bloqueia |
|---|---|---|---|
| WhatsApp (Baileys) | `IWhatsAppService` | nada | avisos de pedido |
| Focus NFe | `IFocusNfeService` | nada | emissão de nota fiscal |
| Brevo | — | **nada** | não integrado (ver `02`) |

---

## Imagens de produto — armazenamento local (implementado)

A vitrine deixou de depender de Cloudinary. As imagens ficam no próprio servidor, em uma
pasta **irmã** da aplicação, para que o deploy via FTP nunca as sobrescreva:

```
root/
├── api.lumimakeup.com.br/    → publicação da API
└── imagens/                  → fora do deploy
```

A leitura pública acontece por `imagens.lumimakeup.com.br`, que aponta via DNS para a
pasta `imagens`. O upload passa pela API (para autenticar e validar); a leitura é servida
diretamente pelo servidor web, sem passar pelo app server.

O banco guarda apenas o **caminho relativo** (`produtos/abc123.jpg`), nunca o caminho
absoluto do servidor, então o dado continua válido se a estrutura mudar.

| Camada | Onde |
|---|---|
| Contrato | `IArmazenamentoDeImagens` em `Application/Abstractions/IIntegrations.cs` |
| Implementação | `ArmazenamentoDeImagensLocal` em `Infrastructure/Integrations` |
| CRUD de produtos/categorias | `GestaoDeProdutosService`, `GestaoDeCategoriasService` |
| Rotas | `api/admin/produtos`, `api/admin/categorias` (exigem `SomenteAdministrador`) |

Configuração em `ArmazenamentoDeImagens`:

| Chave | Padrão | Papel |
|---|---|---|
| `CaminhoBase` | `""` | **obrigatória**. Absoluta em produção; relativa ao *content root* localmente |
| `PastaPadrao` | `produtos` | subpasta dentro de `CaminhoBase` |
| `TamanhoMaximoEmBytes` | `5242880` | 5 MB |
| `ExtensoesPermitidas` | `jpg`, `jpeg`, `png` | extensões liberadas |

Regras de validação no upload:

- **O formato é decidido pelo conteúdo, não pelo nome.** Os primeiros bytes são
  comparados com as assinaturas de JPEG (`FF D8 FF`) e PNG (`89 50 4E 47 0D 0A 1A 0A`).
  Um `.png` disfarçado é recusado com 400.
- A extensão do arquivo gravado vem do formato detectado, nunca da enviada.
- O nome do arquivo é um GUID gerado pelo servidor. O nome original é saneado e
  guardado em `NomeOriginal` só para exibição.
- Toda gravação passa por `ResolverCaminhoSeguro`, que rejeita `..` e qualquer caminho
  que resolved para fora de `CaminhoBase`.
- Máximo de **3 imagens por produto**.
- Excluir imagem ou produto apaga o arquivo do disco; se o `SaveChanges` falhar depois
  de gravar, o arquivo é removido para não deixar órfão.

> **Obrigatório no servidor:** desabilitar execução de scripts na pasta `imagens`.
> Sem isso, um `.aspx`/`.php` enviado por uma sessão comprometida seria executado.
> Isso é configuração do servidor web, não do código. E as imagens precisam entrar no
> plano de backup: elas não têm mais cópia em serviço de terceiros.

---

## Melhoria de texto com IA (implementado)

No painel de administração, o campo de descrição do produto tem um botão que reescreve
o texto por um modelo de linguagem. O endpoint é `POST api/admin/produtos/texto/melhorar`,
protegido pela policy `SomenteAdministrador`, e devolve
`{ descricaoMelhorada, modeloUsado }`.

| Camada | Onde |
|---|---|
| Contrato | `IMelhoradorDeTextoService` em `Application/Abstractions` |
| Implementação | `MelhoradorDeTextoGemini` em `Infrastructure/Integrations` |
| Token | `IProvedorDeTokenDoGoogle` / `ProvedorDeTokenPorAdc` |

**A chave nunca sai do backend.** O navegador só chama a API; é ela que fala com o
Google. É o mesmo cuidado do reCAPTCHA, e vale ainda mais aqui porque a chave tem
custo associado quando o projeto tem faturamento.

**Dois modos de autenticação**, com a chave da AI Studio tendo precedência quando
preenchida. Ver [`02-configuracao-e-ambiente.md`](02-configuracao-e-ambiente.md) para
a tabela completa e a diferença de custo entre eles.

### O prompt proíbe a IA de inventar

O ponto mais importante do prompt: reescrever texto de cosmético é uma operação com
risco regulatório. Alegação de benefício sem respaldo é infração de consumo, e o modelo,
treinado para vender, adiciona benefício se perguntado de forma vaga. Por isso as
regras são explícitas:

- não inventar característica, ingrediente, textura ou benefício;
- não usar promessa de resultado garantido nem linguagem de efeito médico;
- não citar porcentagem, selo, aprovação, certificação ou estudo clínico;
- não inventar número de cores, gramas, volume ou duração;
- devolver **somente** a descrição, sem comentário sobre o que mudou.

Ainda assim, o texto gerado entra no formulário **como sugestão editável**, nunca
gravado direto no produto. Revisão humana antes de salvar é requisito, não formalidade.

### Limites

Descrição entre 10 e 4.000 caracteres, nome até 150. `thinkingLevel: low` porque o
botão espera resposta rápida, e `maxOutputTokens: 1024`.

> A geração passa pela IA, então o texto original e o reescrito saem da sua
> infraestrutura. Para descrição de produto isso não é dado sensível, mas é uma
> decisão consciente do negócio, não um detalhe técnico.



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

1. ~~**Imagens de produto**~~ — concluído (armazenamento local, ver acima).
2. **Checkout e pedidos** — modelo de endereço já pronto (ver `05`); é o que gera
   receita.
3. **WhatsApp** — curto, e melhora a percepção do cliente sobre o pedido.
4. **Focus NFe** — por último, por envolver conformidade.

---

## Como verificar o que é stub

Nomes terminados em `Stub` são implementações fictícias:

```
EmailSenderStub.cs
FocusNfeServiceStub.cs
WhatsAppServiceStub.cs
```

A exceção é `EmailSenderStub`: ele é um **fallback real**, escolhido por `DependencyInjection`
quando o SMTP não está configurado (ver [`11-envio-de-email-smtp.md`](11-envio-de-email-smtp.md)).
