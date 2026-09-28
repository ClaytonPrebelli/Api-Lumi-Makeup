# 16 - CI/CD e deploy

A API é publicada em **produção** pela action `.github/workflows/deploy.yml`, que roda a
cada **push na branch `deploy`**. O destino é a pasta `api.lumimakeup.com.br/`, servida
pelo **IIS** com o ASP.NET Core Module.

## Fluxo

```
push na branch deploy
   └─ testar     dotnet test -> 294 testes
        └─ publicar    dotnet publish -c Release -> publicacao/
             └─ enviar   FTP em duas passadas -> health check
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
| `ConnectionStrings:ConexaoPadrao` | **derruba o deploy** — a API nem sobe |
| `Jwt:Segredo` | **derruba o deploy** — e sem ele a API subiria sem autenticação nenhuma |
| `ExternalServices:Recaptcha:ChaveSecreta` | loga aviso e aceita cadastro sem validação |
| `ExternalServices:Smtp:*` | cai no `EmailSenderStub` — reset de senha não envia e não avisa |
| `ExternalServices:Google:SegredoCliente` | login social não funciona |
| `ExternalServices:Ia:Chave` | o botão de melhorar descrição responde "IA não configurada" |
| `ExternalServices:Brevo:*`, `Baileys:*`, `FocusNfe:*` | stubs, sem efeito enquanto não houver integração |

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

## Por que o deploy baixa a API antes de enviar

O deploy tem **duas passadas de FTP**, e isso não é redundância. É o que faz funcionar.

Enquanto o `w3wp.exe` está no ar, ele segura os arquivos da aplicação abertos e **o FTP
não consegue sobrescrever as DLLs** — a transferência falha no meio, deixando a pasta
mixada: metade da versão antiga, metade da nova.

A solução oficial do ASP.NET Core Module é o **`app_offline.htm`**. Um arquivo com esse
nome na raiz da aplicação faz o módulo desligar a aplicação; enquanto ele existe, o
módulo responde todas as requisições com o conteúdo dele; quando some, a aplicação sobe
de novo no próximo pedido. É o mecanismo que o Web Deploy usa.

O workflow usa exatamente isso, com o arquivo versionado em
`.github/deploy/app_offline.htm`:

| Passo | O que faz |
|---|---|
| `Baixar a API` | FTP só com o `app_offline.htm`. A aplicação para. |
| `Esperar a API parar` | faz `GET /api/saude` até a resposta ser a página de manutenção |
| `Enviar a publicacao` | FTP com o artefato inteiro. Como o `app_offline.htm` **não** está no artefato, a action o apaga — e a aplicação sobe sozinha. |

O passo `Esperar a API parar` não é um `sleep` fixo. Ele pergunta para a API se ela já
parou, e só então envia. Se não confirmar em 60 segundos, o deploy **continua assim mesmo**
e deixa um aviso no log — porque a pasta pode já estar parada, e o único sintoma de
falha real vai ser o envio recusando os arquivos.

> **Se o envio falhar com "arquivo em uso"**, a aplicação não parou. A causa é quase sempre
> o módulo não ter conseguido desligá-la a tempo. As saídas, em ordem de esforço: dar
> **Reciclar** no pool de aplicações pelo Gerenciador do IIS e rodar o deploy de novo;
> ou, se acontecer sempre, definir `ASPNETCORE_FILE_WATCHER_THREAD_TERMINATION` como `1`
> no sistema. Uma conexão aberta (WebSocket) também segura o desligamento — foi o que
> atrasou o `app_offline.htm` até o .NET 8 corrigir.

> **Se a conferência final falhar**, a pasta `app_offline.htm` provavelmente ficou no
> servidor. Ela fica respondendo no lugar da API até ser apagada. O log do workflow diz
> exatamente qual arquivo remover, e é uma pasta só, pela interface de arquivos do
> servidor.

## Segredos e variáveis

### Segredos (criptografados, ficam mascarados no log)

Além dos três de FTP:

| Nome | Vai para |
|---|---|
| `CONEXAO_PADRAO` | `ConnectionStrings:ConexaoPadrao` |
| `JWT_SEGREDO` | `Jwt:Segredo` |
| `RECAPTCHA_CHAVE_SECRETA` | `ExternalServices:Recaptcha:ChaveSecreta` |
| `IA_CHAVE` | `ExternalServices:Ia:Chave` |
| `SMTP_HOST` | `ExternalServices:Smtp:Host` |
| `SMTP_PORTA` | `ExternalServices:Smtp:Porta` — número; vazio assume `465` |
| `SMTP_USUARIO` | `ExternalServices:Smtp:Usuario` |
| `SMTP_SENHA` | `ExternalServices:Smtp:Senha` |
| `GOOGLE_SEGREDO_CLIENTE` | `ExternalServices:Google:SegredoCliente` |
| `BREVO_CHAVE_API` | `ExternalServices:Brevo:ChaveApi` |
| `BAILEYS_SEGREDO_COMPARTILHADO` | `ExternalServices:Baileys:SegredoCompartilhado` |
| `FOCUSNFE_ID_CLIENTE` | `ExternalServices:FocusNfe:IdCliente` |
| `FOCUSNFE_SEGREDO_CLIENTE` | `ExternalServices:FocusNfe:SegredoCliente` |

> **Não existe mais `SEED_ADMINISTRADOR_EMAIL` / `SEED_ADMINISTRADOR_SENHA`.** O seed do
> administrador foi retirado da inicialização quando a produção passou a usar o mesmo
> banco do desenvolvimento — o admin já existe lá. A classe `DatabaseSeeder` continua no
> código, mas nada a chama. Ver [`01-fundacao-da-api.md`](01-fundacao-da-api.md).

> **Chave em *Variable* em vez de *Secret* aparece em texto puro no log.** Todos os itens
> desta tabela são segredos, mesmo os de integração que ainda não está em uso.

### Variáveis (texto plano, para o que não é segredo)

| Variável | Padrão | Para que serve |
|---|---|---|
| `DIRETORIO_DA_API` | `api.lumimakeup.com.br/` | pasta de destino, relativa à raiz do FTP |
| `ENDERECO_DA_API` | `https://api.lumimakeup.com.br` | endereço das conferências |

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
| **Não aplica migrations** | a API não migra o banco na inicialização (ver `00`). Migration é `dotnet ef database update`, na mão, com backup antes. Automatizar isso colocaria o banco na mesma corrida do deploy. |
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
