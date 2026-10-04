# API Lumi Makeup

## Deploy

**A aplicação precisa ser parada antes de cada deploy.** O IIS segura as DLLs
carregadas e a sobrescrita do arquivo é recusada enquanto o Application Pool
está de pé. Quem para é o Clayton, manualmente.

### A ordem, que não pode ser invertida

1. **Parar a aplicação** no painel da hospedagem. Sem isso o FTP falha ao
   sobrescrever as DLLs, e o workflow acusa erro mesmo com tudo certo.
2. **Disparar o workflow** (push na `main`, ou `Actions → Deploy da API →
   Run workflow`).
3. **Levantar a aplicação de novo**, ainda pelo painel.
4. Conferir em `https://api.lumimakeup.com.br/api/saude` se quiser.

O passo 4 é opcional e manual. **O workflow não faz health check**, e a ausência
é proposital: enquanto o pool está parado, a API está fora do ar por padrão, e
um check nesse intervalo falha sempre — faz parecer que o deploy quebrou quando
o único problema é a aplicação estar parada.

Por isso: **não verificar se a API está no ar depois do deploy.** Se o usuário
disser que o deploy foi feito, considere feito.

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

### PROIBIDO: apagar o banco ou tabela

**Nunca**, em nenhuma hipótese, executar `DROP DATABASE`, `DROP TABLE`,
`dotnet ef database drop`, `dotnet ef database update 0` para "recomeçar", nem
qualquer SQL equivalente (`TRUNCATE` também conta). Não importa o motivo, não
importa se o erro parece óbvio, não importa se o usuário pedir.

Essa regra existe porque o custo é o negócio inteiro, não o banco:

- O `DROP DATABASE` já foi executado neste projeto **várias vezes** e apagou
  usuários, clientes, categorias, banners, produtos, imagens, pedidos,
  despesas e a configuração do WhatsApp.
- **Os dados não voltaram.** Migration recria *estrutura* vazia, nunca
  *conteúdo*. Não existe "recuperar os dados depois".
- A recreation por migration dá a ilação de que o banco voltou, e não voltou:
  as tabelas existem e estão vazias, o que é pior que um erro visível.

Se um schema estiver inconsistente, a resposta é **corrigir com migration**:
`ALTER TABLE`, `migrationBuilder.Sql(...)`, coluna nova, índice novo. Nunca
derrubando o banco para recomeçar.

Diagnóstico de esquema é **leitura**: `SHOW CREATE TABLE`, `SHOW TABLE STATUS`,
`information_schema`, `SELECT * FROM __EFMigrationsHistory`. Ler não quebra
nada.

### Toda tabela é InnoDB, criada por migration

- Toda tabela nova **tem que ser `InnoDB`**. MyISAM é proibido.
- Toda tabela nova **tem que ser criada por migration**. Não há script manual
  avulso, não há SQL suelto, não há tabela criada "só pra testar".
- A migration é a única fonte de verdade do schema.

O motivo é técnico e já custou uma incidente: **MyISAM não suporta foreign key**.
MySQL recusa a FK com `errno 150` ("Foreign key constraint is incorrectly
formed") e o erro não diz que a engine está errada — manda procurar nome de
coluna e tipo, que estão perfeitos, e o diagnóstico trava.

Quando a FK falhar com 150, a **primeira** coisa a verificar é a engine das duas
tabas, nas duas pontas:

```sql
SHOW TABLE STATUS LIKE 'tabela';
```

Para converter uma tabela já existente, dentro de uma migration, **sem drop**:

```csharp
migrationBuilder.Sql("ALTER TABLE `minha_tabela` ENGINE = InnoDB;");
```

Isso converte preservando todos os dados. É a operação correta e deve ser
preferida a qualquer alternativa destrutiva.

Nome de coluna, tipo e collation devem bater exatamente dos dois lados da FK.
`utf8mb4_general_ci` em tudo, engine InnoDB em tudo.

### Antes de rodar qualquer migration

1. Ler a migration gerada arquivo por arquivo. Ela é código.
2. Conferir se ela só **acrescenta** (`AddColumn`, `CreateTable`, `CreateIndex`).
   Se aparecer `DropColumn`, `DropTable`, `RenameTable` ou `DropForeignKey`
   inesperado, parar e confirmar com o usuário.
3. Se a migration envolve FK, confirmar `InnoDB` nas duas tabelas antes.

Qualquer dúvida sobre segurança da migration: perguntar. Não executar "para
ver o que acontece".

## Artefato do deploy

O FTP envia um pacote plano, e o workflow falha se sobrar qualquer subdiretório
em `publicacao` — o upload com pasta aninhada derruba a conexão no meio e deixa
a publicação pela metade.

O passo que removia `publicacao\runtimes` **foi removido de propósito** e não
deve ser reintroduzido: ele apagava o nativo do SQLite junto, e foi o que
derrubou a loja. O `-r win-x64` no `dotnet publish` resolve o `runtimes/` na
origem, achata o nativo para a raiz e dispensa a limpeza.

`App_Data` e `logs` estão no `exclude` do FTP. Apagá-los destruiria a sessão do
WhatsApp e o diagnóstico de startup.

O `web.config` é lido pelo IIS **antes de qualquer código da aplicação**. Uma
mudança ali derruba a loja inteira, não um recurso. Ver
[ESTADO-DOS-SERVICOS.md](ESTADO-DOS-SERVICOS.md), seção 4, para o incidente de
2026-10-01 e o que não fazer.

## Documentação

[ESTADO-DOS-SERVICOS.md](ESTADO-DOS-SERVICOS.md) é a referência sobre o que a API
usa de verdade: MySQL sem SQLite, sem Hangfire, WhatsApp por Baileys com stub
como padrão, e o estado do frete e do upload de imagem. Confirme cada afirmação
contra o código antes de confiar — o documento cita arquivo e linha.
