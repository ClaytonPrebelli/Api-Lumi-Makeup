# 16 - CI/CD e deploy

A API é publicada em **produção** pela action `.github/workflows/deploy.yml`, que roda a
cada **push na branch `main`**. O destino é a pasta `api.lumimakeup.com.br/`, servida
pelo **IIS** com o ASP.NET Core Module.

> **O workflow precisa existir na `main` para o push na `main` disparar ele.** O GitHub
> procura os workflows no ref que está sendo enviado, não em qualquer branch. Enquanto o
> arquivo morar só na `deploy`, trocar o gatilho para `main` não dispara nada — é o
> motivo de o primeiro merge da `deploy` na `main` ser obrigatório, e não opcional.

## Fluxo

```
push na branch main
   └─ testar     dotnet test -> 294 testes
        └─ publicar    dotnet publish -c Release -> publicacao/
             └─ enviar   FTP -> health check
```

Três jobs, na ordem. O deploy só acontece se os **294 testes** passarem, e a pasta
`publicacao/` que vai para o servidor é exatamente a que o job anterior produziu.

## Os segredos vêm do GitHub e viram configuração no servidor

Nenhuma chave está no Git, e nenhuma é digitada no servidor. O step
**Montar o appsettings de producao** gera o `appsettings.Production.json` a partir dos
**secrets do GitHub** e o coloca no meio da publicação. O ASP.NET Core lê esse arquivo
**por cima** do `appsettings.json` que está no repositório, que continua sendo a camada
de valores padrão, sem nenhum segredo.

O resultado é que o servidor tem um único arquivo com toda a configuração sensível, e ele
é **reconstruído a cada deploy** a partir do GitHub. Girar uma chave é trocar o secret e
dar push na `deploy` — não é editar arquivo nenhum no servidor.

### Por que só duas chaves derrubam o deploy

O step falha com mensagem explícita quando `CONEXAO_PADRAO` ou `JWT_SEGREDO` não estão
cadastradas, e **não publica nada**. As outras chaves vão com string vazia quando não
estão cadastradas, que é exatamente o que o `appsettings.json` do repositório já traz —
ou seja, a integração simplesmente continua desligada, do jeito que foi projetada.

| Chave | Sem ela |
|---|---|
| `BANCO_SERVIDOR` | **derruba o deploy** — a API nem sobe |
| `BANCO_NOME` | **derruba o deploy** — idem |
| `BANCO_USUARIO` | **derruba o deploy** — idem |
| `BANCO_SENHA` | **derruba o deploy** — idem |
| `JWT_SEGREDO` | **derruba o deploy** — e sem ele a API subiria sem autenticação nenhuma |
| `ExternalServices:Recaptcha:ChaveSecreta` | loga aviso e aceita cadastro sem validação |
| `ExternalServices:Smtp:*` | cai no `EmailSenderStub` — reset de senha não envia e não avisa |
| `ExternalServices:Google:SegredoCliente` | login social não funciona |
| `ExternalServices:Ia:Chave` | o botão de melhorar descrição responde "IA não configurada" |
| `ExternalServices:Brevo:*`, `Baileys:*`, `FocusNfe:*` | stubs, sem efeito enquanto não houver integração |

> **A connection string é montada pelo workflow, não colada.** Não existe um secret
> `CONEXAO_PADRAO`: entram `BANCO_SERVIDOR`, `BANCO_PORTA`, `BANCO_NOME`, `BANCO_USUARIO` e
> `BANCO_SENHA` separados, e o step monta
> `Server=...;Port=...;Database=...;Uid=...;Pwd=...;SslMode=...;AllowPublicKeyRetrieval=...`.
> É a mesma forma que o SMTP já usava no `appsettings.json`, e evita o erro de colar a
> string inteira com um `;` a mais ou faltando.

> **Senha com `;`, aspas ou espaço é tratada.** O `;` é o separador da connection string,
> então uma senha `abc;def` colada na mão quebraria a configuração em silêncio — a API
> subiria e o acesso ao banco falharia com "acesso negado", sem nenhuma pista de que o
> problema era a string. O step coloca aspas automaticamente quando o valor tem `;`,
> `"` ou espaço, e escapa a aspa interna duplicando, que é o que o MySqlConnector aceita
> (barra invertida **não** é escape aqui). O mesmo vale para servidor, banco e usuário.
>
> E o `.strip()` é aplicado em host, porta, nome, usuário e opções, mas **não** em senha
> nem em chave. Uma senha com espaço no fim é uma senha válida, e aparar as pontas
> transformaria um segredo correto em um errado — o mesmo "acesso negado" sem
> explicação. Senha só com espaço é tratada como ausente, que é a única exceção.

> **`Jwt:Segredo` é o mais perigoso da lista.** Sem ele a API **sobe** e todos os
> endpoints protegidos ficam acessíveis, sem erro e sem aviso no log — o
> `Program.cs:65` só registra o JWT se o segredo existir. Por isso o deploy trata o
> segredo vazio como motivo para não publicar, em vez de deixar a API subir aberta.

### O segredo nunca entra no artefato

O `appsettings.Production.json` é gerado **no job de envio**, depois que o artefato já
foi baixado. Duas guardas impedem que ele chegue ao `artifact`:

1. O job `publicar` falha se o arquivo estiver no `publicacao/`.
2. O job `enviar` falha de novo, logo depois de baixar o artefato, se ele estiver lá.

O motivo é o risco real: `actions/upload-artifact` deixa o arquivo disponível para
qualquer pessoa com leitura no repositório durante o prazo de retenção. Um
`appsettings.Production.json` no artefato seria a connection string do banco entregue
para quem abrir o repositório.

As chaves chegam ao step por `env:`, e o Python lê `os.environ` — elas nunca são
interpoladas no texto do comando, então não aparecem nem no log nem no diff do step.
Os secrets do GitHub já são mascarados automaticamente na saída.

## O `web.config`

`src/LumiMakeup.Api/web.config` é **versionado** e não gerado pelo `dotnet publish` a
cada deploy, para fixar o ambiente de forma explícita e revisável em pull request:

```xml
<environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
```

Sem essa linha o `appsettings.Production.json` **não é lido** — e como ele é quem carrega
as chaves, a API subiria sem nenhuma delas. É o link entre o arquivo que o deploy gera e
o arquivo que a API efetivamente lê.

> O `dotnet publish` gera um `web.config` quando o projeto não tem um, e ele não
> declara o ambiente. O padrão do IIS é `Production`, então a API funcionaria por
> coincidência. Declarar remove a coincidência da equação.

## O artefato precisa ser plano

O deploy falha com `ECONNRESET` se a publicação tiver ** subdiretórios**, e isso aconteceu
duas vezes antes de ser descoberto.

A action de FTP abre **uma conexão de dados nova por diretório criado**, e o servidor
descarta a partir da terceira. O frontend e o outro projeto .NET deste servidor publicam
listas planas de arquivos, por isso nunca tiveram o problema — a diferença nunca foi
volume nem TLS, foi a contagem de diretórios.

A publicação do .NET vinha com `runtimes/win/lib/net7.0/` e `runtimes/win/lib/net8.0/`,
621 KB e quatro níveis, que davam exatamente a terceira conexão que derruba.

A pasta foi removida da publicação, e ela é dispensável: numa publicação **sem `-r`**, quem
carregado é a cópia portátil dos assemblies, que fica na **raiz** — `System.Management.dll`
e `System.Security.Cryptography.Pkcs.dll` já estão lá. A pasta `runtimes/` guarda só a
variante específica por RID. Confirmado na execução: com ela removida, a API sobe,
`/api/saude` responde 200 e `/api/produtos` falha por **conexão** com o banco
(`MySqlConnector.MySqlException`), não por assembly faltando — que é o que aconteceria se
fosse realmente necessária.

O step `Conferir o artefato` **falha o deploy se aparecer qualquer subdiretório**, dizendo
quais. O motivo é não depender de alguém lembrar dessa regra: se uma dependência nova
trazer uma pasta aninhada, o erro aparece como `ECONNRESET` no meio de um log de FTP, que
não liga a causa ao diretório. Falhando ali, o nome da pasta vem junto.

## Arquivos travados: recicle o pool antes de publicar

Enquanto o `w3wp.exe` está no ar, ele segura os arquivos da aplicação abertos e **o FTP
não consegue sobrescrever as DLLs**. A transferência falha no meio, deixando a pasta
mixada: metade da versão antiga, metade da nova.

> **Por que o workflow não resolve isso sozinho.** A solução oficial do ASP.NET Core
> Module é o `app_offline.htm` — um arquivo com esse nome na raiz da aplicação faz o
> módulo desligar a aplicação, e a subida seguinte a religa. O deploy usava isso em duas
> passadas de FTP, mas foi removido a pedido: ele exibia uma página de manutenção para o
> cliente durante a publicação, e a manutenção é mais do que a operação precisa mostrar.
>
> A consequência é direta: **o primeiro deploy funciona, porque a pasta está vazia. Os
> seguintes só funcionam se o pool de aplicações for reciclado antes.** Sem isso o envio
> falha com "arquivo em uso" logo nas primeiras DLLs, e a pasta fica no estado misto.

O que fazer, em ordem:

1. **Reciclar o pool de aplicações** pelo Gerenciador do IIS (ou pelo painel do seu
   servidor) **antes** de rodar o deploy. É o passo que substitui o `app_offline.htm`.
2. Se o painel tiver a opção de reiniciar a aplicação a cada publicação, use-a — é o
   mesmo efeito, automatizado.
3. Se o erro persistir depois de reciclar, definir `ASPNETCORE_FILE_WATCHER_THREAD_TERMINATION`
   como `1` no sistema. Uma conexão aberta (WebSocket) também segura o desligamento.

A primeira publicação da API não passa por nenhum desses problemas, porque ainda não há
arquivo no servidor para ser travado. Vale saber disso antes de concluding que está tudo
certo depois de um deploy bem-sucedido.

> **Se a conferência final falhar**, as causas mais prováveis, em ordem: o envio falhou e
> sobrou arquivo antigo; a aplicação subiu mas o `appsettings.Production.json` está
> errado (veja `ConnectionStrings` e `Jwt`); ou o pool precisa ser reciclado para carregar
> a versão nova. A mensagem do workflow lista as três.

## Segredos e variáveis

### Segredos (criptografados, ficam mascarados no log)
**Tudo é secret.** Não há nenhuma *variable* neste repositório: pasta de destino e
endereço de conferência também são secrets, porque é mais simples ter um lugar só e não
existe risco em mascarar um caminho.

Além dos três de FTP, o banco e as chaves. `CONEXAO_PADRAO` **não existe** — a string é
montada a partir dos oito campos de `BANCO_*`:

| Nome | Vai para |
|---|---|
| `BANCO_SERVIDOR` | `Server=` da connection string |
| `BANCO_PORTA` | `Port=` — vazio assume `3306` |
| `BANCO_NOME` | `Database=` |
| `BANCO_USUARIO` | `Uid=` |
| `BANCO_SENHA` | `Pwd=` |
| `BANCO_CHARSET` | `CharSet=` — vazio assume `utf8mb4` |
| `BANCO_SSL` | `SslMode=` — vazio assume `Preferred` |
| `BANCO_ALLOW_PUBLIC_KEY` | `AllowPublicKeyRetrieval=` — vazio assume `true` |
| `JWT_SEGREDO` | `Jwt:Segredo` |
| `RECAPTCHA_CHAVE_SECRETA` | `ExternalServices:Recaptcha:ChaveSecreta` |
| `IA_CHAVE` | `ExternalServices:Ia:Chave` |
| `SMTP_HOST` | `ExternalServices:Smtp:Host` |
| `SMTP_PORTA` | `ExternalServices:Smtp:Porta` — vazio assume `465` |
| `SMTP_USUARIO` | `ExternalServices:Smtp:Usuario` |
| `SMTP_SENHA` | `ExternalServices:Smtp:Senha` |
| `GOOGLE_SEGREDO_CLIENTE` | `ExternalServices:Google:SegredoCliente` |
| `BREVO_CHAVE_API` | `ExternalServices:Brevo:ChaveApi` |
| `BAILEYS_SEGREDO_COMPARTILHADO` | `ExternalServices:Baileys:SegredoCompartilhado` |
| `FOCUSNFE_ID_CLIENTE` | `ExternalServices:FocusNfe:IdCliente` |
| `FOCUSNFE_SEGREDO_CLIENTE` | `ExternalServices:FocusNfe:SegredoCliente` |
| `DIRETORIO_DA_API` | pasta de destino, relativa à raiz do FTP — vazio assume `api.lumimakeup.com.br/` |
| `ENDERECO_DA_API` | endereço do health check — vazio assume `https://api.lumimakeup.com.br` |

> **`CharSet=utf8mb4` não é decoração.** É o que permite gravar emoji e acentuação completa na
> descrição do produto. Sem ele, o texto quebra ao salvar — e o problema aparece no
> painel, dias depois do deploy, sem relação aparente com ele.

> **Não existe mais `SEED_ADMINISTRADOR_EMAIL` / `SEED_ADMINISTRADOR_SENHA`.** O seed do
> administrador foi retirado da inicialização quando a produção passou a usar o mesmo
> banco do desenvolvimento — o admin já existe lá. A classe `DatabaseSeeder` continua no
> código, mas nada a chama. Ver [`01-fundacao-da-api.md`](01-fundacao-da-api.md).

> **Chave em *Variable* em vez de *Secret* aparece em texto puro no log.** Por isso
> **nada** aqui é variable, nem as pastas e endereços. Todos os itens desta tabela são
> secrets, mesmo os de integração que ainda não está em uso.

### Variables (texto plano, para o que não é segredo)

Nenhuma. Todas as configurações deste repositório são secrets — a tabela acima está
completa. Pasta de destino e endereço de conferência também são secrets: é mais simples
ter um só lugar, e mascarar um caminho não custa nada.

O job de envio roda no **ambiente `producao`** do GitHub, criado automaticamente na
primeira execução, e que depois pode exigir aprovação manual.

> **Use `ftps`, não `ftp`.** Antes as chaves ficavam só no servidor e o FTP trafegava
> apenas código. Agora o `appsettings.Production.json` atravessa a conexão, e em `ftp`
> puro ele vai pela rede em texto legível — junto com a senha do banco. Não é teoria,
> é a senha do MySQL e o segredo de assinatura do JWT. Se `FTPS` falhar ao conectar, use
> `ftps-legacy`, que é a forma quase sempre liberada em hospedagem compartilhada.

## O que o deploy não faz

| O que | Por quê |
|---|---|
| **Não aplica migrations, nunca** | a API não migra o banco na inicialização, e o workflow não tem nenhum passo de banco. Aplicação é local, em desenvolvimento, contra o mesmo banco que serve a produção — o servidor não recebe schema. Ver [`04-banco-de-dados-e-ef-core.md`](04-banco-de-dados-e-ef-core.md). |
| **Não envia `.pdb`** | são ~4 MB por assembly e só servem para depurar com símbolos. Estão no `exclude` da action. |
| **Não publica o `appsettings.Production.json` do repositório** | o arquivo é sempre gerado no deploy. Se alguém versionar um por engano, o `.gitignore` bloqueia e as guardas do workflow falham. |

A lista de `exclude` da action **substitui** o valor padrão dela, então as regras padrão
(`.git*`, `node_modules`) estão escritas de novo ao lado. Tirar uma linha de lá faz o
arquivo passar a ser enviado.

## Arquivos antigos e rollback

A action guarda o que **ela mesma** enviou da última vez e, no deploy seguinte, apaga do
servidor só esses arquivos. Logs, o que o admin deixou na pasta e qualquer
`appsettings.Production.json` manual **não são tocados** — e o `dangerous-clean-slate`,
que apagaria a pasta inteira, é `false` por padrão e não é usado.

O rollback do código é criar uma branch a partir do commit anterior, copiar o workflow
para ela e dar push. **O do `appsettings.Production.json` é o mesmo deploy**: como o
arquivo é gerado a cada publicação, voltar a versão anterior do código já restaura a
configuração daquele deploy.

## Deploy manual

**Actions → Deploy da API → Run workflow** roda o mesmo caminho a partir da branch
`deploy`. Com `dry-run: true` no passo `Enviar a publicacao`, a action imprime o que
mudaria sem enviar nada.
