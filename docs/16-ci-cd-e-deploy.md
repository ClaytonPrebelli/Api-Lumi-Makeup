# 16 - CI/CD e deploy

A API é publicada em **produção** pela action `.github/workflows/deploy.yml`, que roda a
cada **push na branch `deploy`**. O destino é a pasta `api.lumimakeup.com.br/`, servida
pelo **IIS** com o ASP.NET Core Module.

## Fluxo

```
push na branch deploy
   └─ testar     dotnet test -> 296 testes
        └─ publicar    dotnet publish -c Release -> publicacao/
             └─ enviar   FTP em duas passadas -> health check
```

Três jobs, na ordem. O deploy só acontece se os **296 testes** passarem, e a pasta
`publicacao/` que vai para o servidor é exatamente a que o job anterior produziu.

## Os segredos não estão no deploy

`appsettings.Production.json` fica **só no servidor**. Ele não está no repositório — o
`.gitignore` já o exclui — e o workflow tem uma verificação que **falha o build** se esse
arquivo aparecer no artefato:

```csharp
if (Test-Path 'publicacao\appsettings.Production.json')
{
    throw 'appsettings.Production.json entrou no artefato. Os segredos ficam so no servidor.'
}
```

> Isso não é exagero. O `appsettings.json` que **está** no repositório tem
> `"ConexaoPadrao": ""` e `"Segredo": ""` — é só a camada de base, com valores vazios de
> propósito. Quem preenche é o `appsettings.Production.json` do servidor, que o ASP.NET
> Core lê **por cima** dele. Se algum dia alguém subir esse arquivo com a connection string
> preenchida, o `git push` leva a senha do banco para o histórico do GitHub. A verificação
> existe para esse arquivo nunca ser aceito, mesmo por engano.

Para o `ASPNETCORE_ENVIRONMENT` valer `Production` — e com isso o `appsettings.Production.json`
ser lido — o `web.config` **está versionado** em `src/LumiMakeup.Api/web.config`, e não
gerado pelo `dotnet publish` a cada deploy:

```xml
<environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
```

> O `dotnet publish` gera um `web.config` quando o projeto não tem um, e ele **não**
> define o ambiente — o que significa `Production` por padrão no IIS, mas só porque
> `web.config` gerado não mexe nisso. Versionar o arquivo deixa o ambiente **explícito e
> revisável em pull request**, em vez de depender do que o IIS já tinha configurado.

### O que precisa existir no servidor

Chaves do `appsettings.Production.json`, descritas em
[`02-configuracao-e-ambiente.md`](02-configuracao-e-ambiente.md):

| Chave | Para que serve |
|---|---|
| `ConnectionStrings:ConexaoPadrao` | MySQL de produção |
| `Jwt:Segredo` | assina o token — **obrigatório**, sem ele a API sobe sem autenticação |
| `ExternalServices:Smtp:*` | e-mail do reset de senha |
| `ExternalServices:Recaptcha:ChaveSecreta` | valida o reCAPTCHA |
| `ExternalServices:Google:SegredoCliente` | login social |
| `ExternalServices:Ia:Chave` | reescrever descrição de produto |
| `Autenticacao:SeedAdministrador:Email` / `:Senha` | só na primeira subida |

A pasta `imagens/` fica **irmã** da pasta da aplicação, nunca dentro dela — é o que
mantém as fotos do produto fora do que o deploy publica. Ver
[`13-integracoes-pendentes.md`](13-integracoes-pendentes.md).

## Por que o deploy baixa a API antes de enviar

O deploy tem **duas passadas de FTP**, e isso não é redundância. É o que faz funcionar.

Enquanto o `w3wp.exe` está no ar, ele segura os arquivos da aplicação abertos e **o FTP
não consegue sobrescrever as DLLs** — a transferência falha no meio, deixando a pasta
mixada: metade da versão antiga, metade da nova.

A solução oficial do ASP.NET Core Module é o **`app_offline.htm`**. Um arquivo com esse
nome na raiz da aplicação faz o módulo desligar a aplicação; enquanto ele existe, o módulo
responde todas as requisições com o conteúdo dele; quando some, a aplicação sobe de novo
no próximo pedido. É o mecanismo que o Web Deploy usa.

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

| Nome | Onde | Para que serve |
|---|---|---|
| `FTP_HOST` | Secret | endereço do servidor de FTP |
| `FTP_USERNAME` | Secret | usuário do FTP |
| `FTP_PASSWORD` | Secret | senha do FTP |
| `FTP_PORT` | Secret *(opcional)* | padrão `21` |
| `FTP_PROTOCOL` | Secret *(opcional)* | `ftp` (padrão), `ftps` ou `ftps-legacy` |

| Variável | Padrão | Para que serve |
|---|---|---|
| `DIRETORIO_DA_API` | `api.lumimakeup.com.br/` | pasta de destino, relativa à raiz do FTP |
| `ENDERECO_DA_API` | `https://api.lumimakeup.com.br` | endereço usado nas conferências |

O job de envio roda no **ambiente `producao`** do GitHub, que é criado automaticamente na
primeira execução e depois pode exigir aprovação manual.

## O que o deploy não faz

| O que | Por quê |
|---|---|
| **Não aplica migrations** | a API não migra o banco na inicialização (ver `00`). Migration é `dotnet ef database update`, na mão, com backup antes. Automatizar isso colocaria o banco na mesma corrida do deploy. |
| **Não envia `.pdb`** | são ~4 MB por assembly e só servem para depurar com símbolos. Excluídos no `exclude` da action. |
| **Não toca no `appsettings.Production.json`** | está na lista de `exclude` do mesmo jeito, como rede de proteção se ele um dia entrar no repositório. |

A lista de `exclude` da action **substitui** o valor padrão dela, então as regras padrão
(`.git*`, `node_modules`) estão escritas de novo ao lado. Tirar uma linha de lá faz o
arquivo passar a ser enviado.

## Arquivos antigos e rollback

A action guarda o que **ela mesma** enviou da última vez e, no deploy seguinte, apaga do
servidor só esses arquivos. `appsettings.Production.json`, arquivos de log e o que o
admin deixou na pasta **não são tocados** — e o `dangerous-clean-slate`, que apagaria a
pasta inteira, é `false` por padrão e não é usado.

O rollback é criar uma branch a partir do commit anterior, copiar o workflow para ela e
dar push.

## Deploy manual

**Actions → Deploy da API → Run workflow** roda o mesmo caminho a partir da branch
`deploy`. Com `dry-run: true` no passo `Enviar a publicacao`, a action imprime o que
mudaria sem enviar nada.
