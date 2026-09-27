# 03 — Nomenclatura e Padronização

**Status:** ✅ concluído

---

## Objetivo

Manter o código inteiro em português, com nomes previsíveis, para que qualquer
desenvolvedor leia o repositório sem traduzir nada.

---

## Português no código

Não existe nenhum identificador em inglês no código de produção.

| Categoria | Convenção | Exemplo |
|---|---|---|
| Entidades | substantivo singular, sem prefixo | `Usuario`, `Pedido`, `ItemPedido`, `NotaFiscal` |
| Enums | substantivo singular, com `Status` quando aplicável | `StatusPedido`, `MetodoPagamento` |
| Services | `AssuntoService` | `AutenticacaoService`, `CatalogoService` |
| Interfaces | `I` + nome do service | `IAutenticacaoService`, `ICatalogoService` |
| Controllers | `AssuntoController` | `AutenticacaoController`, `ProdutosController` |
| Configurações EF | `EntidadeConfiguration` | `PedidoConfiguration` |
| DTOs | `Requisicao` / `Resposta` / substantivo | `RequisicaoDeLogin`, `RespostaDeAutenticacao` |
| Opções | substantivo + `Options` | `TokenJwtOptions`, `SmtpOptions` |
| Métodos | verbo + complemento | `CadastrarAsync`, `ValidarTokenAsync` |

A pasta `Entities/` não tem prefixo `Entity`, e o projeto é `LumiMakeup.Domain` —
não `LumiMakeup.Entities`. O idioma aparece também nos nomes de arquivos de migration
(`RenomearParaPortugues`, `AdicionarRecuperacaoDeSenha`).

---

## Tabelas e colunas

- Tabelas: **snake_case plural** — `pedidos`, `usuarios`, `itens_pedido`,
  `notas_fiscais`, `recuperacoes_de_senha`.
- Propriedades: **PascalCase** — `CustoFrete`, `HashSenha`, `PrecoVendaUnitario`.
- A conversão para snake_case é feita pelo EF com as convenções do Pomelo/MySQL, não
  por atributo em cada propriedade.

A configuração fica sempre em uma classe `IEntityTypeConfiguration<T>` separada, e
não em atributos sobre a entidade. Isso mantém `Domain` livre de dependência do EF —
as entidades são POCOs puros.

---

## Enums

Serializados como **string** no banco e no JSON, e nas duas pontas:

```csharp
builder.Property(o => o.Status)
    .HasConversion<string>()
    .HasMaxLength(30)
    .IsRequired();
```

```csharp
options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
```

O resultado é legível e estável nos dois lados:

```json
{ "status": "Pago", "statusEntrega": "Enviado", "metodoPagamento": "Pix" }
```

O tamanho máximo de 30 caracteres acompanha o nome do membro mais longo de cada
enum. Renomear um membro de enum passa a ser mudança de schema — vale lembrar disso
antes de dar `Ctrl+R` em um enum.

---

## Async sempre

Todo método que fala com I/O termina em `Async` e recebe `CancellationToken`:

```csharp
Task<UsuarioDto> ObterUsuarioAtualAsync(long usuarioId, CancellationToken cancellationToken = default);
```

O `CancellationToken` percorre controllers, services e EF Core sem interrupção
manual. Só dispensa o parâmetro quem não é público (construtores, helpers privados,
`Program.cs`).

---

## Tratamento de erros

A API não usa exceções como fluxo de controle normal. Duas exceções são
deliberadamente capturadas pelos controllers, porque carregam significado de
negócio:

| Exceção | Tradução HTTP | Onde |
|---|---|---|
| `InvalidOperationException` | `409 Conflict` ou `400 BadRequest` | e-mail/CPF já cadastrado, reCAPTCHA inválido |
| `UnauthorizedAccessException` | `401 Unauthorized` | credenciais inválidas, token expirado |

> `CompletarPerfil` devolve `400` para `InvalidOperationException`, enquanto
> `Cadastrar` devolve `409`. A distinção é semântica: no cadastro o e-mail já
> existente é **conflito de estado**; no perfil é **requisição inválida**.

Erros de infraestrutura (Falha de rede, SMTP, ViaCEP) são registrados em log pelo
serviço e convertidos em resposta neutra — a mensagem interna nunca chega ao cliente.

---

## Métodos privados estáticos

Helpers que não usam estado da instância são `private static`, sinalizando que são
puros:

```csharp
private static string GerarToken() { ... }
private static string CalcularHashDoToken(string token) { ... }
private static UsuarioDto ParaUsuarioDto(Usuario usuario) { ... }
```

---

## Convenções de teste

- Um arquivo de teste por área, espelhando o nome do arquivo testado.
- Nomes no formato `Sujeito_verbo_complemento`:

  ```csharp
  [Fact] public void Cadastrar_normaliza_o_email_e_define_o_papel_cliente()
  [Fact] public void Pedido_define_propriedades_e_relacoes()
  [Fact] public void RecaptchaValidator_recusa_token_quando_o_score_fica_abaixo_do_limite()
  ```

- Arrange / Act / Assert separados por linha em branco.
- `Assert.Equal` para valores, `Assert.Null` / `Assert.NotNull` para nulos.
