# API Lumi Makeup

## Deploy

**A aplicação precisa ser parada antes de cada deploy.** O IIS segura as DLLs
carregadas e a sobrescrita do arquivo é recusada enquanto o Application Pool
está de pé. Quem para é o Clayton, manualmente.

Por isso: **não verificar se a API está no ar depois do deploy.** A janela entre
parar a aplicação, enviar os arquivos e subir de novo deixa a API fora do ar por
padrão, e um health check nesse intervalo só gera alarme falso. Se o usuário
dissipar que o deploy foi feito, considere feito.

Depois de um deploy da API, a pasta de imagens pode ter ficado inconsistente com
o banco. Vale um olhar só se o usuário pedir.

## Verbo HTTP

O servidor de produção **só encaminha GET, POST, HEAD, OPTIONS e TRACE**. `PUT` e
`DELETE` são recusados pelo IIS *antes de chegar na API*, com 405 e sem nenhum
header de CORS — o navegador reporta isso como "bloqueado pela política de CORS",
que é a mensagem errada e esconde a causa.

Toda operação que altera ou apaga é `POST` com o verbo no fim da URL
(`/atualizar`, `/excluir`, `/ordem`, `/ativo`).
`tests/LumiMakeup.Tests/Api/VerboHttpDasRotasTests.cs` trava essa regra: ele falha
se um controller do painel declarar `PUT`, `DELETE` ou `PATCH`. **Não "corrigir"
para DELETE sem ler esse teste.**

## Banco de dados

O banco de desenvolvimento **é o mesmo de produção**.

- Migration **nunca** roda no deploy nem no servidor. Ela é gerada e aplicada
  localmente, e o efeito aparece em produção junto.
- Antes de aplicar, pensar duas vezes: qualquer alteração de schema é pública
  no mesmo instante.

## Artefato do deploy

O FTP envia um pacote plano. O workflow remove `publicacao\runtimes` e falha se
sobrar sub diretório — o upload com pasta aninhada derruba a conexão no meio e
deixa a publicação pela metade.
