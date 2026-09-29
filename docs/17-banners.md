# 17 - Banners do hero

**Status:** concluído

---

## Objetivo

Expor o carrossel que abre a home: a vitrine lê os banners **ativos** e o painel os
administra. Cada slide carrega **duas** imagens, uma de desktop e uma de celular.

---

## Por que duas imagens e não uma

A proporção muda demais entre 1600px e 375px. A arte de desktop é bem horizontal
(1600x600) e a de celular quase vertical (900x1200). Servir a mesma imagem para os
dois casos daria, no celular, um recorte minúsculo no meio da tela ou a arte
esticada.

Guardar as duas deixa o **navegador** escolher pela tag `picture`:

```html
<picture>
  <source media="(max-width: 820px)" srcset="banners/m1.png" />
  <img src="banners/d1.png" alt="..." />
</picture>
```

Nenhuma decisão de viewport acontece em JavaScript, e a arte de celular **não é
baixada** no desktop.

---

## Endpoints

### Público

| Método | Rota | Resposta |
|---|---|---|
| `GET` | `/api/banners` | Banners **ativos**, ordenados |

Sem `[Authorize]` - a vitrine precisa funcionar para quem ainda não entrou na conta.
Devolve **lista vazia**, nunca `404`, quando não há banner ativo: a home cai no hero
de texto, e um `404` ali viraria um site sem topo.

### Painel

| Método | Rota | Resposta |
|---|---|---|
| `GET` | `/api/admin/banners` | Todos, ativos ou não, ordenados |
| `POST` | `/api/admin/banners` | Cria (multipart, **duas** imagens) |
| `POST` | `/api/admin/banners/{id}/atualizar` | Atualiza (imagens opcionais) |
| `POST` | `/api/admin/banners/{id}/ativo` | Liga ou desliga o slide |
| `POST` | `/api/admin/banners/{id}/excluir` | Remove slide e as duas imagens |
| `POST` | `/api/admin/banners/ordem` | Reordena o carrossel |

**Todos as mutações são `POST`.** O servidor de produção não encaminha `PUT` nem
`DELETE` - ver [`16-ci-cd-e-deploy.md`](16-ci-cd-e-deploy.md) e
`VerboHttpDasRotasTests`.

---

## Limite de 3 slides

`IGestaoDeBannersService.MaximoDeBanners` é `3`, e a API **recusa** o quarto
banner. É limite rígido, não aviso: o layout da home foi desenhado para três, e o
quarto empurra o restante para fora da dobra em telas de celular.

A mensagem de recusa diz o que fazer (`Exclua um existente ou desative-o`),
porque o número sozinho não orienta.

O contador vale para o total, ativo ou não. Desativar **não** libera a posição:
o slide continua ocupando um dos três lugares, e é isso que mantém a ordem estável
quando ele volta a ser ativado.

---

## As duas imagens sobem no mesmo multipart

```
POST /api/admin/banners
  imagemDesktop: <arquivo>   (obrigatório)
  imagemMobile:  <arquivo>   (obrigatório)
  textoAlternativo: <texto>
```

Duas chamadas separadas permitiriam o estado em que o banner existe **pela metade**
— com a arte de desktop mas sem a de celular. A home não sabe exibir isso: a tag
`picture` ficaria com um `<source>` sem `srcset`. Por isso o controller exige os
dois antes de chamar o serviço.

Na **atualização** os dois são opcionais: quem só quer corrigir o texto
alternativo não tem os arquivos originais à mão, e a API mantém as artes atuais
quando o campo não vem. Quem manda um arquivo vazio no lugar de outro troca só
aquele.

`textoAlternativo` vazio é enviado como campo presente e vazio, não como campo
ausente — a API distingue os dois, e só sobrescreve o texto quando o campo chega.

---

## Arquivo órfão: o risco real do upload duplo

Gravar arquivos no disco e gravar a linha no banco são duas operações, e a segunda
pode falhar depois da primeira. A pasta `banners/` é criada no primeiro upload
(`Directory.CreateDirectory` antes de gravar), então nada disso depende de
preparação manual no servidor.

O serviço trata os quatro pontos de falha:

| Situação | O que acontece |
|---|---|
| Upload do mobile falha | O arquivo do desktop, já gravado, é apagado |
| `SaveChanges` falha ao criar | Os dois arquivos são apagados |
| `SaveChanges` falha ao trocar | As imagens novas são apagadas; as antigas ficam |
| Troca de imagem com sucesso | O arquivo **antigo** é apagado, depois do `SaveChanges` |

Sem isso, `banners/` acumularia arquivos que nenhum registro referencia — e o
painel não tem como listá-los, porque só o cadastro sabe o caminho.

A ordem importa na troca: o arquivo antigo só sai **depois** que o banco aceitou.
Apagar antes deixaria o banner sem imagem caso a gravação falhasse.

A exclusão de arquivo é **melhor esforço**. Arquivo já sumido, ou caminho inválido
armazenado, são sucesso: o que precisa sair é a referência no banco. Deixar a
exceção escapar transformaria uma exclusão concluída em erro no painel, por causa
de um arquivo que só ocupa espaço.

---

## Pasta própria no armazenamento

`IArmazenamentoDeImagens` ganhou `ArmazenarEmPastaAsync`, e `ArmazenarAsync`
continua gravando na `PastaPadrao` (`produtos`).

É um **método separado**, e não um parâmetro opcional em `ArmazenarAsync`. Tentei
o parâmetro primeiro e ele quebrou três specs: árvore de expressão do Moq não
aceita argumento omitido (`CS0854`). Além disso, o nome fica mais honesto — quem
chama diz em qual pasta quer gravar.

Sem a pasta separada, os banners acabariam gravados dentro de `produtos/`,
misturados com as fotos dos produtos.

A pasta pedida passa pelo mesmo `SanearPasta` da pasta padrão: `".."` e barra
invertida são recusados, e `"../../segredo"` vira `segredo` porque o ponto não é
um caractere aceito.

---

## Reordenação exige a lista inteira

`ReordenarAsync` **recusa** lista parcial ou com id inexistente
(`InvalidOperationException`). Mandar só os dois banners trocados deixaria os
demais com a ordem antiga, e o painel mostraria uma sequência diferente da que
ficou gravada - sem nenhum erro visível.

A igualdade é de conjunto: mesma quantidade, e todo id do banco presente.

A ordem é gravada como a **posição** (`Ordem = 0, 1, 2`), não como um número
escolhido pelo cliente. Assim não há "ordem 47" órfã quando um banner é excluído
no meio da lista.

---

## Ativar e desativar em vez de excluir

`Ativo` tira o slide do ar sem apagar nada: um banner sazonal volta a aparecer em
segundos, e os arquivos continuam no lugar.

Por isso desativar não libera vaga (ver [limite de 3](#limite-de-3-slides)) - a
coerência do carrossel depende das posições ficarem estáveis.

---

## Vitrine: `ICatalogoService`, não `IGestaoDeBannersService`

O endpoint público passa por `ICatalogoService.ObterBannersAtivosAsync`, e não pelo
serviço de gestão. A vitrine pública não deve enxergar banner inativo nem os dados
de administração.

O DTO da vitrine (`BannerDto`) sai sem `NomeOriginalDesktop`, `NomeOriginalMobile`
e `CriadoEm`. O nome do arquivo no disco do cliente é detalhe de organização
interna, e exibi-lo não agrega nada para quem compra.

`BannerAdministracaoDto` carrega os dois, porque o painel precisa mostrar as duas
miniaturas e reenfileirar a troca.

---

## Onde as regras são travadas por teste

| Regra | Teste |
|---|---|
| Nenhum verbo bloqueado nos controllers do painel | `VerboHttpDasRotasTests` |
| Rotas mutadoras são `POST` com o verbo na URL | `VerboHttpDasRotasTests` |
| Quarto banner recusado | `GestaoDeBannersServiceTests` |
| Arquivo órfão apagado em upload parcial e em `SaveChanges` recusado | `GestaoDeBannersServiceTests` |
| Troca de imagem não remove a arte que continua em uso | `GestaoDeBannersServiceTests` |
| Reordenação recusa lista parcial e id inexistente | `GestaoDeBannersServiceTests` |
| Vitrine esconde inativo e não expõe dados de administração | `CatalogoServiceTests` |
| `ArmazenarEmPastaAsync` grava na pasta pedida e neutraliza travessia | `ArmazenamentoDeImagensLocalTests` |
| `ArmazenarAsync` continua na pasta padrão | `ArmazenamentoDeImagensLocalTests` |

---

## Ver também

- [`12-catalogo.md`](12-catalogo.md) - catálogo público
- [`15-gestao-de-produtos.md`](15-gestao-de-produtos.md) - mesmo padrão de gestão, com as
  rotas em `POST`
- [`13-integracoes-pendentes.md`](13-integracoes-pendentes.md) - armazenamento local de imagens
- [`16-ci-cd-e-deploy.md`](16-ci-cd-e-deploy.md) - deploy e restrição de verbos
