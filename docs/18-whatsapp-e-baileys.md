# 18 - WhatsApp e a frase que abre a mensagem

**Status:** concluído - o Node do Baileys e a edição da frase no painel. Falta fechar o
segredo, que está no arquivo versionado (ver
[Segredo no repositorio](#o-segredo-do-baileys-está-no-arquivo-versionado))

---

## Objetivo

A administradora escrever, pelo painel, a frase que abre a mensagem de WhatsApp do
cliente, e ver **antes de salvar** exatamente como a mensagem vai chegar no aparelho
dela.

---

## Por que a frase mora no banco e não no `appsettings`

Quem escreve a frase é a administradora, pelo painel, no meio da operação - muitas
vezes porque um cliente reclamou do tom. Um arquivo de configuração não se edita sem
republicar a API, e republicar exige parar a aplicação. Uma frase que exige deploy
para mudar é uma frase que ninguém muda.

Por isso a frase fica em `configuracao_whatsapp`, e
`NotificacoesDePedido:MensagemInicialWhatsAppCliente` continua existindo **apenas como
padrão de fábrica**, para a loja funcionar no primeiro dia sem ninguém ter digitado
nada.

| Fonte | Quando é usada |
|---|---|
| Linha em `configuracao_whatsapp` | Sempre que a administradora já salvou uma frase |
| `NotificacoesDePedido:MensagemInicialWhatsAppCliente` | Enquanto a tabela estiver vazia |

> **Tabela vazia é estado inicial, e não é erro.** `GestaoDeWhatsAppService.ObterAsync`
> devolve o padrão do `appsettings` quando não há linha, e `SalvarAsync` faz upsert na
> primeira linha em vez de inserir sempre. A tabela pode passar a vida inteira com uma
> linha só.

---

## Endpoints

Todos em `AdminWhatsAppController`, sob a policy `SomenteAdministrador`, e **todos as
mutações são `POST`** - o servidor de produção não encaminha `PUT` nem `DELETE`
(ver [`16-ci-cd-e-deploy.md`](16-ci-cd-e-deploy.md)).

| Método | Rota | O que faz |
|---|---|---|
| `GET` | `/api/admin/whatsapp/status` | Node no ar e número pareado |
| `POST` | `/api/admin/whatsapp/pareamento` | QR em PNG base64 |
| `POST` | `/api/admin/whatsapp/pareamento/reconectar` | Força nova tentativa de pareamento |
| `GET` | `/api/admin/whatsapp/mensagem` | Lê a frase vigente |
| `POST` | `/api/admin/whatsapp/mensagem` | Grava a frase |
| `GET` | `/api/admin/whatsapp/mensagem/previa?mensagemInicial=` | Monta a mensagem sem gravar |

Fora do painel, para cliente logado (`WhatsappController`, `[Authorize]` sem
policy de admin):

| Método | Rota | O que faz |
|---|---|---|
| `GET` | `/api/whatsapp/status` | Mesma leitura de status. O front chama ao abrir o carrinho e ao adicionar item, e o tráfego de entrada acorda o Node — na hora de finalizar, a confirmação já encontra o serviço de pé |

Respostas:

```json
{ "id": 1, "mensagemInicialCliente": "Oi, {Nome}! Aqui é da Lumi Makeup. Recebemos seu pedido!" }
```

```json
{ "mensagem": "Oi, Maria! Aqui é da Lumi Makeup. Recebemos seu pedido!\n\n*Itens do Pedido*\n..." }
```

`POST /mensagem` responde `400` com `{ "message": "..." }` quando a frase está vazia ou
passa de 500 caracteres. A validação fica no serviço, e não como atributo de
modelo, porque a mensagem é uma regra de negócio e o texto que a pessoa lê é mais útil
que um erro genérico de validação.

| Situação | Mensagem |
|---|---|
| Frase vazia ou só espaços | "Escreva a frase que abre a mensagem do cliente." |
| Mais de 500 caracteres | "A frase pode ter no máximo 500 caracteres." |

---

## A prévia usa o mesmo código da mensagem real

Este é o ponto que sustenta o resto do documento. `MontadorDeMensagemDePedido` é
chamado tanto por `NotificadorDePedido` (que envia) quanto por
`GestaoDeWhatsAppService.GerarPreviaAsync` (que mostra). Uma única implementação, e
não duas parecidas.

```
frase da administradora  ─┐
                          ├─→ MontadorDeMensagemDePedido ─┬─→ NotificadorDePedido ─→ Node Baileys ─→ cliente
Pedido real              ─┘                                └─→ GET /mensagem/previa ─→ painel
```

Se a prévia fosse montada no Angular ou numa cópia do formato, as duas coisas divergem
na primeira frase que a administradora trocar - e a divergência apareceria em produção,
no aparelho do cliente, que é onde ela não pode ser corrigida.

> **Por que o `Montador` é uma classe estática separada do `NotificadorDePedido`.** Ele
> não tem dependência: nem `DbContext`, nem `IWhatsAppService`, nem `IOptions`. Isso é
> o que permite o mesmo método ser chamado do fluxo de envio e do endpoint de prévia sem
> montar nenhuma infraestrutura no meio.

`GerarPreviaAsync` monta a mensagem sobre `MontadorDeMensagemDePedido.PedidoDeExemplo()`,
com dois itens, um desconto e um endereço completo. O objetivo não é o texto, é o
**tamanho**: a administradora precisa ver se a mensagem cabe, onde os valores caem e
quão longa fica antes de gravar a frase.

A prévia manda a frase **que está no campo**, não a que está gravada. Prévia da frase
salva só ajudaria depois de salvar, que é tarde demais para quem está decidindo o que
escrever. Sem frase na query, o serviço usa a gravada.

---

## Marcadores aceitos

| Marcador | Vira |
|---|---|
| `{Nome}` | Primeiro nome do cliente |
| `{Loja}` | `NotificacoesDePedido:NomeDaLoja` |

Marcador desconhecido **some em silêncio** e não quebra o envio no meio. Uma mensagem
que falha inteira por causa de um `{Preco}` que ninguém documentou seria pior do que
uma mensagem com um trecho faltando, e a falha apareceria no aparelho do cliente.

O `{Nome}` usa só o primeiro nome. Nome completo em WhatsApp é o primeiro texto que se
lê, e fica cortado no meio da tela quando o aparelho é pequeno.

---

## Negrito é asterisco simples, e isso não é descuido

```
Oi, {Nome}! Aqui é da Lumi Makeup. Recebemos seu pedido!

*Itens do Pedido*
• 1x Batom Matte - R$ 35,90
• 1x Pó Compacto - R$ 45,90

*Subtotal:* R$ 81,80
*Desconto (LUMI10):* R$ 10,00
*Frete:* R$ 0,00
*Total:* R$ 91,80

*Endereço de entrega:*
Avenida Paulista, 1000 - Conj. 101
Bela Vista - São Paulo/SP
CEP: 01310-000

Confirme seu pedido acima se está tudo certo por favor. É só responder esta mensagem.
```

O WhatsApp renderiza negrito com **um** asterisco. Com `**` - a sintaxe do Markdown -
os dois asteriscos aparecem literalmente no texto do cliente. A mensagem foi montada,
enviada, e o resultado foi `**Itens do Pedido**` na tela de quem estava esperando uma
loja. Por isso os títulos saem com asterisco único.

Os títulos que a montadora controla são fixos: `*Itens do Pedido*`, `*Subtotal:*`,
`*Desconto:*`, `*Frete:*`, `*Total:*` e `*Endereço de entrega:*`. A frase da
administradora entra no topo e não é mexida - inclusive os asteriscos, que são
decisão dela.

O bloco de endereço é omitido inteiro quando o pedido não tem endereço, porque pedido de
balcão não tem logradouro e a mensagem não pode inventar um.

---

## Camadas

| Camada | Onde |
|---|---|
| Entidade | `ConfiguracaoWhatsApp` em `Domain/Entities` |
| Mapeamento | `ConfiguracaoWhatsAppConfiguration` em `Persistence/Configurations` |
| Contrato | `IGestaoDeWhatsAppService` em `Application/Abstractions/ICatalogoService.cs` |
| Implementação | `GestaoDeWhatsAppService` em `Infrastructure/Services` |
| Montagem do texto | `MontadorDeMensagemDePedido` em `Infrastructure/Services` |
| Envio | `NotificadorDePedido` em `Infrastructure/Services` |
| Rotas | `AdminWhatsAppController` em `Api/Controllers` |

---

## Como o cliente é registrado

`RegistrarWhatsApp` em `DependencyInjection.cs:121` decide o que entra no container, e a
decisão é tomada **uma vez**, na inicialização:

| Condição | O que é registrado |
|---|---|
| `Habilitado` falso, ou `SegredoCompartilhado` vazio | `WhatsAppServiceStub` |
| Habilitado, com segredo, `IniciarProcesso` falso | `BaileysWhatsAppService` por HTTP |
| Habilitado, com segredo, `IniciarProcesso` true | idem + `SupervisorDeNodeBaileys` |

O stub é o padrão **de propósito**: o WhatsApp é acessório, e a loja precisa vender do
mesmo jeito sem ele. Ver [`13-integracoes-pendentes.md`](13-integracoes-pendentes.md).

`EstadoDoNodeBaileys` é singleton porque o supervisor escreve o motivo da falha e a tela
lê: são duas pontas do mesmo processo, e o estado não pode depender do escopo do request.

`/pareamento` não expõe o Node na internet. Quem pede o QR é a API, com o segredo, e o
navegador recebe pela API. Expor o Node significaria que qualquer página aberta por baixo
consumiria o QR de acesso à conta.

---

## Configuração

| Chave | Valor no `appsettings.json` | Papel |
|---|---|---|
| `NotificacoesDePedido:WhatsAppDaAdministradora` | `5515991475568` | número da loja nos avisos |
| `NotificacoesDePedido:NomeDaLoja` | `Lumi Makeup` | valor de `{Loja}` |
| `ExternalServices:Baileys:Habilitado` | `true` | liga o cliente real |
| `ExternalServices:Baileys:IniciarProcesso` | `false` | supervisor **não** sobe o Node |
| `ExternalServices:Baileys:UrlBase` | `http://localhost:3001` | base do Node |
| `ExternalServices:Baileys:Porta` | `3001` | porta, quando a URL é omitida |

> **`IniciarProcesso` é `false` em produção, e é a decisão certa.** O Node roda em
> serviço próprio; a API falar com ele por HTTP é mais previsível do que a API ficar
> responsável por subir e vigiar um processo Node. Em desenvolvimento ele roda na mão,
> com `npm start`.

O número configurado em `WhatsAppDaAdministradora` **não é o número que envia**. Quem
envia é o número pareado na sessão do Baileys. Os dois campos são independentes e
confundi-los produz um alerta que vai para o número certo mas responde do número errado.

### O segredo do Baileys está no arquivo versionado

`ExternalServices:Baileys:SegredoCompartilhado` está com valor literal em
`appsettings.json`, que é versionado. Isso está **errado** e precisa ser corrigido: o
segredo autentica a API perante o Node, e qualquer pessoa com leitura do repositório
pode falar com o Node.

O caminho correto é o mesmo das outras chaves: secret do repositório, montado pelo passo
"Montar o appsettings de producao" do `deploy.yml`, a partir de `BAILEYS_SEGREDO`. O
`appsettings.json` versionado deve ficar com a chave **vazia**.

> **Dois passos, não um.** Além de mover para o secret, o valor atual precisa ser
> **rotacionado** no Node. Ele já está no histórico do Git, então trocar o lugar não
> basta: o valor antigo continua válido para quem o leu. Enquanto isso não for feito,
> o segredo no repositório é um segredo comprometido - e o doc `02` trata as chaves de
> produção dessa forma.

---

## O que não é testado

`MontadorDeMensagemDePedido` não tem arquivo de teste próprio. É exercitado
**indiretamente**, pelas asserções de `GestaoDeWhatsAppServiceTests.cs`: troca de
marcadores, títulos com asterisco, endereço omitido e presença dos totais. Os casos
passam porque a prévia sai do mesmo método que monta a mensagem real.

> **É uma lacuna conhecida, e não uma escolha fechada.** O `Montador` é uma classe
> estática sem dependências — o caso mais fácil de testar da API inteira. O que falta é
> o arquivo `MontadorDeMensagemDePedidoTests.cs`, com os casos que hoje dependem de
> passar pela prévia: formatação de moeda em `pt-BR`, `Subtotal` sem desconto, e
> complemento e CEP ausentes no endereço.

O `BaileysWhatsAppService` também não é testado falando com o Node real. Seria um teste
de integração com um processo Node do lado, e o que ele verificaria é o contrato do
outro lado, não a lógica da loja. O `IWhatsAppService` já tem
`BaileysWhatsAppServiceTests.cs` com HttpClient mockado, o supervisor tem
`SupervisorDeNodeBaileysTests.cs`, e o `AdminWhatsAppController` é exercitado pela
regra de verbo HTTP em `VerboHttpDasRotasTests`.

---

## Ver também

- [`13-integracoes-pendentes.md`](13-integracoes-pendentes.md) - Focus NFe, a integração que continua stub
- [`02-configuracao-e-ambiente.md`](02-configuracao-e-ambiente.md) - chaves de produção e secrets
- [`04-banco-de-dados-e-ef-core.md`](04-banco-de-dados-e-ef-core.md) - tabela `configuracao_whatsapp`
- [`11-envio-de-email-smtp.md`](11-envio-de-email-smtp.md) - o mesmo pedido chega também por e-mail
- `LumiMakeup/docs/26-mensagem-de-whatsapp.md` - a tela no painel