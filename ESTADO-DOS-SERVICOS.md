# Estado dos servicos

Documento de referencia sobre o que a API usa de verdade, e o que NAO usa.
Escrito depois do incidente de 2026-10-01, em que um deploy derrubou a loja
inteira com HTTP 500.30.

Confirme cada item neste documento contra o codigo antes de confiar nele. As
fontes estao citadas em cada secao.

---

## 1. O que a API usa

### Banco: MySQL, e so MySQL

`AddDbContext<LumiDbContext>` com `UseMySql`, em
`src/LumiMakeup.Infrastructure/DependencyInjection.cs:29-30`.

A string de conexao vem do secret `BANCO_*`, montada pelo passo "Montar o
appsettings de producao" em `.github/workflows/deploy.yml`. Ela nunca vai para
o repositorio: o passo "Conferir o artefato" falha o deploy se
`appsettings.Production.json` aparecer no artefato.

**Nao ha SQLite.** Nao ha `e_sqlite3.dll` no csproj e o passo "Conferir o
artefato" falha o build se esse arquivo aparecer na publicacao
(`deploy.yml:115-117`). O teste local confirma: 56 arquivos, zero
`e_sqlite3.dll`.

### WhatsApp: Node Baileys por HTTP, com stub como padrao

`RegistrarWhatsApp` em `DependencyInjection.cs:120-161`. A decisao depende de
`ExternalServices:Baileys`:

| Condicao | O que e registrado |
|---|---|
| `Habilitado` falso, ou `SegredoCompartilhado` vazio | `WhatsAppServiceStub` |
| Habilitado, com segredo, `IniciarProcesso` falso | `BaileysWhatsAppService` por HTTP |
| Habilitado, com segredo, `IniciarProcesso` true | idem + `SupervisorDeNodeBaileys` como hosted service |

O stub e o padrao de proposito: o WhatsApp e acessorio, e a loja precisa vender
do mesmo jeito sem ele. O supervisor so e registrado quando `IniciarProcesso` e
true - em desenvolvimento o Node roda na mao, com `npm start`.

O endpoint do Node vem do secret `BAILEYS_URL_BASE`, porque o Render sorteia o
nome do servico na criacao. A porta vem da mesma secao, e sao as duas que tem
que concordar: divergir entre elas so produz "nao conecta"
(`DependencyInjection.cs:141-149`).

`EstadoDoNodeBaileys` e singleton de proposito: o supervisor escreve o motivo
da falha e a tela le. Sao duas pontas do mesmo processo, entao o estado nao pode
depender do escopo do request (`DependencyInjection.cs:127`).

### Frete: cadeia de provedores, com o Nominatim no fim

`CalculoDeFreteService` depende de `IGeocodificador`, nao de `INominatimService`
(`CalculoDeFreteService.cs:29,31`), e chama `GeocodificarAsync` na linha 56.

A implementacao registrada e `GeocodificadorEmCadeia`
(`Integrations/IGeocodificador.cs:27`), com `BaseAddress` do Photon
(`DependencyInjection.cs:39-41`).

A cadeia tenta, nesta ordem:

| Ordem | Provedor | Consulta |
|---|---|---|
| 1 | Photon (komoot) | `postalcode {cep}` |
| 2 | ArcGIS | `findAddressCandidates` por CEP |
| 3 | Photon (komoot) | endereco completo |

O Nominatim continua no codigo e continua falhando com o proxy de producao - ver a
secao 3. Ele saiu da posicao de unica opcao, e nao do codigo.

### Outros servios registrados

| Servico | Registro | Observacao |
|---|---|---|
| ViaCEP | `DependencyInjection.cs:34-36` | CEP, `https://viacep.com.br/` |
| Recaptcha | `DependencyInjection.cs:58` | validacao de cadastro |
| Melhorador de texto (IA) | `DependencyInjection.cs:64` | gera descricao de produto |
| E-mail SMTP | `DependencyInjection.cs:45-56` | `SmtpEmailSender` se `ExternalServices:Smtp:Host` estiver preenchido, senao `EmailSenderStub` |
| Focus NFe | `DependencyInjection.cs:76` | `FocusNfeServiceStub` - stub, nao integrado |
| Imagens | `DependencyInjection.cs:72` | `ArmazenamentoDeImagensLocal`, grava em disco |
| Google OAuth | `DependencyInjection.cs:102` | login social |
| Seeder | `DependencyInjection.cs:106` | `DatabaseSeeder` |

`DatabaseSeeder` e scoped, **nao** hosted service. Nao ha `AddHostedService`
 alem de `SupervisorDeNodeBaileys`, e `Program.cs` nao chama `Migrate` nem
`EnsureCreated` na inicializacao. A API sobe sem tocar no schema.

---

## 2. O que a API nao usa

### Hangfire: nao existe

Nao ha pacote, nao ha `AddHangfire`, nao ha `IAgendamentoDeJobs` registrado. Os
servicos de e-mail de recompra que dependiam de agenda foram removidos junto com
o Hangfire.

**Nao reintroduzir.** O Hangfire derrubou a loja uma vez: ele traz o
`e_sqlite3.dll` como dependencia, e a publicacao sem `-r win-x64` deixava o
nativo preso em `runtimes/`, o que dava `DllNotFoundException` antes de qualquer
middleware e 500.30 em todos os endpoints.

### SQLite: nao existe

Ver secao 1. O banco e MySQL.

### Photon, ArcGIS, GeocodificadorEmCadeia: nao existem

Estes foram adicionados no commit `ee4e291` e **revertidos** em 2026-10-01. Nao
ha `IGeocodificador`, nem `GeocodificadorEmCadeia`, nem
`tests/.../GeocodificadorEmCadeiaTests.cs` na arvore atual.

Esse commit chegou a ser verificado localmente e produzia CEP 18072-856 em
Sorocaba com 2,17 km e R$ 5,00. Ele foi revertido por estar no mesmo deploy que
derrubou a loja, e nao porque o codigo estivesse errado. Se alguem recuperar
`ee4e291` do historico, saiba disso: o codigo funciona, mas nao pode ir para
producao junto com a mudanca de `web.config` que derrubou o servico.

---

## 3. O que esta quebrado agora

### Resolvido nesta etapa

Os dois itens abaixo estavam nesta secao e foram fechados. Ficam registrados aqui
porque o diagnostico e' util, nao porque continuem abertos.

**Frete - corrigido com cadeia de provedores.** `NominatimService` nao fecha TLS
com o servidor de producao (proxy responde `HandshakeFailure`), e isso matava o
calculo inteiro. A correcao esta em `GeocodificadorEmCadeia`
(`Integrations/IGeocodificador.cs`), que tenta Photon por CEP, ArcGIS por CEP, e
Photon pelo endereco completo, nessa ordem. A ordem nao e' por preferencia:
Photon devolve o logradouro quando o endereco tem rua e o CEP exato quando nao tem.
Nenhum provedor inventa posicao - se todos recusarem, o calculo recusa.

**Upload de imagem - agora retorna o erro.** `IOException`,
`UnauthorizedAccessException`, `DbUpdateException` e `Exception` sao tratados, e o
`Access-Control-Allow-Origin` passa a ser aplicado mesmo em falha, para que o
navegador nao acuse CORS onde o problema e' disco ou permissao.
`Directory.CreateDirectory` esta dentro do `try`.

### Segredo do Baileys no arquivo versionado

`ExternalServices:Baileys:SegredoCompartilhado` esta com valor literal em
`src/LumiMakeup.Api/appsettings.json`, que e' versionado. Isso contraria a regra do
proprio `docs/02-configuracao-e-ambiente.md`.

O segredo autentica a API perante o Node do Baileys. Quem tem leitura do repositorio
consegue falar com o Node e puxar o QR de pareamento da conta.

Dois passos, e o segundo e' o que costuma ser esquecido:

1. Mover a chave para o secret do repositorio (`BAILEYS_SEGREDO`), montado pelo
   passo "Montar o appsettings de producao" em `deploy.yml`, e deixar a chave
   **vazia** no `appsettings.json`.
2. **Rotacionar o valor no Node.** Ele ja esta no historico do Git; trocar o lugar
   nao torna o valor antigo invalido. Sem rotacionar, o segredo continua
   comprometido por leitura do repositorio.

---

## 4. O incidente de 2026-10-01

Sequencia, para quem chegar depois e nao querer repetir:

1. `833da8f` + `ee4e291` + `7068816` foram enviados juntos. `833da8f` ligou
   `stdoutLogEnabled="true"` no `web.config`.
2. A API passou a responder **HTTP 500.30 - "ASP.NET Core app failed to
   start"** em todos os endpoints, inclusive `/api/saude`. A aplicacao morria ao
   iniciar, antes de qualquer codigo rodar.
3. **A causa nunca foi confirmada.** A hipotese levantada foi que o ANCM exige
   que a pasta `.\logs` exista e seja gravavel pela conta do application pool
   quando `stdoutLogEnabled` e' true - e ela nao existe no servidor, porque o
   deploy exclui `logs` de proposito. Isso e' plausivel e nao foi provado: o
   stdout do servidor nunca foi lido, porque exige derrubar a aplicacao.
4. `e280df1` reverteu o `web.config` para a versao do `63846bd`.
5. Os quatro commits foram revertidos. A arvore esta identica ao `63846bd`, o
   commit que ficou no ar antes de tudo.

**A licao que vale:** o `web.config` e lido pelo IIS antes de qualquer codigo da
aplicacao. Uma mudanca ali derruba a loja inteira, nao um recurso. E um
diagnostico que so aparece depois da queda custa mais do que o diagnostico que
se perde.

**Como nao repetir:** mudar `web.config` ou `deploy.yml` em um deploy sozinho, e
nao no mesmo deploy de mudanca de regra de negocio. Ler o log de eventos do
Windows do servidor antes de formar hipotese - e' o que fecha o diagnostico de
500.30, e custa menos que derrubar a loja para descobrir.

---

## 5. Como validar antes de qualquer deploy

O que foi usado no incident, e que pega erro de startup que `dotnet build` nao
pega:

```powershell
dotnet build LumiMakeup.slnx -c Release          # tem que dar 0 Erro(s)
dotnet test  LumiMakeup.slnx -c Release          # tem que dar Com falha: 0
dotnet publish src/LumiMakeup.Api/LumiMakeup.Api.csproj `
  -c Release -r win-x64 --self-contained false -o publicacao
# conferir: arquivos no topo, zero subdiretorios, sem e_sqlite3.dll
```

O `-r win-x64` e' obrigatorio, nao otimizacao: sem ele o publish traz `runtimes/`
com um assembly por arquitetura e o unico nativo do projeto fica preso ali. E o
que daria `DllNotFoundException` e 500.30.

Depois de publicar, **subir a API de verdade** e chamar `/api/saude`,
`/api/produtos` e `/api/banners`. Compilar nao prova que a aplicacao sobe; o
500.30 deste incidente passou pelo build e pelos 540 testes.

**O processo de deploy exige a aplicacao parada antes do envio** - o IIS segura
as DLLs carregadas e recusa a sobrescrita com o pool no ar. Quem para e o
Clayton, manualmente. Por isso nao existe health check no workflow: a API fica
fora do ar por padrao entre o fim do FTP e o pool ser levantado, e um health
check nesse intervalo falharia sempre.

**Verbos:** esta API nao usa `PUT` nem `DELETE`. O servidor de producao so
encaminha GET, POST, HEAD, OPTIONS e TRACE; um PUT e recusado pelo IIS antes de
chegar ao codigo, com 405 e sem header de CORS. Toda operacao que muda ou apaga
algo e um POST com o verbo no fim da URL: `/atualizar`, `/excluir`.
