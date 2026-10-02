using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Application.DTOs;

public sealed record CategoriaDto(long Id, string Nome, string Slug, string? Descricao, bool Ativo);

public sealed record ImagemProdutoDto(long Id, string CaminhoRelativo, string NomeOriginal, int Ordem);

public sealed record ProdutoAdministracaoDto(
    long Id,
    string Nome,
    string Slug,
    string Descricao,
    decimal PrecoCusto,
    decimal PrecoVenda,
    decimal? PrecoPromocional,
    int QuantidadeEstoque,
    bool Ativo,
    bool Destaque,
    DateTime CriadoEm,
    long CategoriaId,
    string NomeCategoria,
    IReadOnlyList<ImagemProdutoDto> Imagens);

public sealed record RequisicaoDeProduto(
    long CategoriaId,
    string Nome,
    string? Slug,
    string Descricao,
    decimal PrecoCusto,
    decimal PrecoVenda,
    decimal? PrecoPromocional,
    int QuantidadeEstoque,
    bool Ativo,
    bool Destaque);

public sealed record RequisicaoDeOrdenacaoDeImagens(IReadOnlyList<long> Ordem);

/// <summary>
/// Banner do hero visto pelo painel. Leva os dois caminhos porque o upload é feito
/// em campos separados e a tela de edicao precisa mostrar os dois arquivos.
/// </summary>
public sealed record BannerAdministracaoDto(
    long Id,
    string CaminhoRelativoDesktop,
    string CaminhoRelativoMobile,
    string NomeOriginalDesktop,
    string NomeOriginalMobile,
    string? TextoAlternativo,
    int Ordem,
    bool Ativo,
    DateTime CriadoEm);

/// <summary>
/// Banner ativo do carrossel, para a vitrine publica. Fica de fora
/// <c>NomeOriginal</c> e <c>CriadoEm</c>: sao dados do painel, e a vitrine nao
/// precisa deles.
/// </summary>
public sealed record BannerDto(
    long Id,
    string CaminhoRelativoDesktop,
    string CaminhoRelativoMobile,
    string? TextoAlternativo,
    int Ordem);

public sealed record RequisicaoDeOrdenacaoDeBanners(IReadOnlyList<long> Ordem);

public sealed record RequisicaoDeAtivacaoDeBanner(bool Ativo);

public sealed record CupomDto(
    long Id,
    string Codigo,
    decimal Percentual,
    int QuantidadeDisponivel,
    decimal ValorMinimo,
    DateTime? ValidadeAte,
    bool Ativo,
    DateTime CriadoEm);

public sealed record RequisicaoDeCupom(
    string Codigo,
    decimal Percentual,
    int QuantidadeDisponivel,
    decimal ValorMinimo,
    DateTime? ValidadeAte,
    bool Ativo);

public sealed record RequisicaoDeSomaDeQuantidadeDeCupom(int Quantidade);

public sealed record RequisicaoDeAtivacaoDeCupom(bool Ativo);

/// <summary>
/// Prévia do cupom no checkout. O subtotal vem do navegador e **não** é
/// conferido: o valor cobrado é recalculado no servidor na criação do pedido.
/// </summary>
public sealed record RequisicaoDeValidacaoDeCupom(string Codigo, decimal SubtotalDosProdutos);

/// <summary>
/// O que um cupom válido produz em um pedido: qual cupom foi, e quanto desconto
/// ele gera sobre o subtotal dos produtos.
///
/// É separado do <see cref="CupomDto"/> porque o painel precisa do cadastro inteiro,
/// com validade e chave ativa, e o checkout só precisa do desconto. O que se
/// calcula no caminho é o desconto; o que se cadastra é a regra.
/// </summary>
public sealed record AplicacaoDeCupom(
    long CupomId,
    string Codigo,
    decimal Percentual,
    decimal Desconto);

public sealed record ItemDePedidoRequisicao(long ProdutoId, int Quantidade);

public sealed record EnderecoDeEntregaRequisicao(
    string Cep,
    string? Logradouro,
    string Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Estado);

/// <summary>
/// Pedido a ser criado, comum ao checkout e à venda de balcão. O que muda entre
/// os dois é a <c>Origem</c> e o que vem preenchido: venda de balcão não traz
/// endereço e o frete é zero.
///
/// <paramref name="CustoFrete"/> entra como valor pronto, e não como cálculo.
/// Calcular a distância e a tarifa é do checkout, e essa regra ainda não existe
/// (ver <c>ConfiguracaoFrete</c> no roadmap). Trazer a fórmula para dentro do
/// pedido deixaria meio-cálculo em dois lugares.
/// </summary>
public sealed record RequisicaoDePedido(
    long UsuarioId,
    IReadOnlyList<ItemDePedidoRequisicao> Itens,
    EnderecoDeEntregaRequisicao? Endereco,
    string? CupomCodigo,
    string? Observacoes,
    decimal CustoFrete,
    decimal DistanciaKm,
    DateTime? CriadoEm);

public sealed record PedidoItemDto(
    long ProdutoId,
    string Nome,
    int Quantidade,
    decimal PrecoVendaUnitario,
    decimal? PrecoPromocionalUnitario,
    decimal Subtotal);

public sealed record PedidoDto(
    long Id,
    long UsuarioId,
    string NomeCliente,
    string? DocumentoCliente,
    string? TelefoneContato,
    string? EmailContato,
    OrigemPedido Origem,
    StatusPedido Status,
    MetodoPagamento? MetodoPagamento,
    string? CupomCodigo,
    decimal Subtotal,
    decimal Desconto,
    decimal CustoFrete,
    decimal Total,
    string? Observacoes,
    DateTime CriadoEm,
    DateTime? PagoEm,
    string? EnderecoCep,
    string? EnderecoLogradouro,
    string? EnderecoNumero,
    string? EnderecoComplemento,
    string? EnderecoBairro,
    string? EnderecoCidade,
    string? EnderecoEstado,
    IReadOnlyList<PedidoItemDto> Itens);

/// <summary>
/// O que o Node do Baileys devolve para a API guardar a sessao.
/// </summary>
/// <param name="VersaoEsperada">
/// A versao que o Node leu. A API recusa a gravacao se o banco ja estiver
/// acima: nesse caso outro contêiner escreveu, e o Node recarrega em vez de
/// sobrescrever.
/// </param>
/// <param name="Credenciais">Blob de credenciais do Baileys.</param>
/// <param name="Chaves">Chaves de sinal em JSON.</param>
public sealed record RequisicaoDeGravacaoDeSessao(
    int VersaoEsperada,
    string Credenciais,
    string? Chaves);

/// <summary>
/// Pedido criado pelo checkout. Não leva <c>UsuarioId</c> nem <c>CustoFrete</c>:
/// o cliente sai do token e o frete é calculado aqui, a partir do endereço.
///
/// Mandar o custo no corpo faria a API confiar no valor do navegador, e um
/// cliente poderia finalizar a compra pagando frete zero.
/// </summary>
public sealed record RequisicaoDeCriacaoDePedido(
    IReadOnlyList<ItemDePedidoRequisicao> Itens,
    EnderecoDeEntregaRequisicao? Endereco,
    string? CupomCodigo,
    string? Observacoes);

/// <summary>
/// Pedido na listagem do painel. Sem os itens: a lista mostra resumo, e puxar os
/// itens de cada linha faria uma consulta por pedido na tela que mais se abre.
/// </summary>
public sealed record PedidoListaDto(
    long Id,
    string NomeCliente,
    string? TelefoneContato,
    OrigemPedido Origem,
    StatusPedido Status,
    MetodoPagamento? MetodoPagamento,
    string? CupomCodigo,
    decimal Total,
    bool NotaFiscalGerada,
    DateTime CriadoEm,
    int QuantidadeDeItens);

/// <summary>
/// Venda de balcão. É a requisição de criação **mais a data**.
///
/// A data é da venda, não da digitação: a administradora registra uma venda de
/// ontem depois. Ela existe só aqui, e não na requisição do checkout, porque
/// nesse caso quem manda é o cliente — e um cliente que pudesse escolher a data
/// do próprio pedido ordenaria a fila de relatórios.
/// </summary>
public sealed record RequisicaoDeVendaDeBalcao(
    long UsuarioId,
    IReadOnlyList<ItemDePedidoRequisicao> Itens,
    string? CupomCodigo,
    string? Observacoes,
    DateTime? CriadoEm);

public sealed record RequisicaoDePagamentoDePedido(MetodoPagamento MetodoPagamento);

/// <summary>
/// Frete de um endereço, para mostrar na tela antes de o cliente fechar.
///
/// <c>PrecoPorKm</c> e <c>TaxaMinima</c> vêm junto para o checkout poder
/// explicar o número em vez de mostrar um total que ninguém entende: "12,40 de
/// frete (7,8 km a R$ 1,20/km, mínimo de R$ 9,00)" diz de onde saiu o valor.
/// </summary>
public sealed record CalculoDeFreteDto(
    decimal DistanciaKm,
    decimal Custo,
    decimal PrecoPorKm,
    decimal TaxaMinima);

/// <summary>
/// Configuração de frete da loja, como o painel mostra e edita.
///
/// <c>Latitude</c> e <c>Longitude</c> não são digitadas: entram pela consulta de
/// CEP, pelo mesmo motivo de bairro e cidade no endereço. Pedir coordenada na
/// tela faria a administradora colar um número de mapa no lugar errado sem
/// nenhuma pista de erro.
/// </summary>
public sealed record ConfiguracaoDeFreteDto(
    long Id,
    string CepOrigem,
    decimal PrecoPorKm,
    decimal TaxaMinima,
    decimal? DistanciaDeExemplo,
    decimal? FreteDeExemplo);

public sealed record RequisicaoDeConfiguracaoDeFrete(
    string CepOrigem,
    decimal PrecoPorKm,
    decimal TaxaMinima);

public sealed record RequisicaoDeSimulacaoDeFrete(string Cep);

  public sealed record ConfiguracaoWhatsAppDto(
      long Id,
      string MensagemInicialCliente);

  public sealed record RequisicaoDeConfiguracaoWhatsApp(
      string MensagemInicialCliente);

  public sealed record PreviaMensagemWhatsAppDto(
      string Mensagem);

/// <summary>Endereço da agenda do cliente, como o checkout lista.</summary>
public sealed record EnderecoDto(
    long Id,
    string Cep,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Estado,
    bool Padrao);

/// <summary>
/// Endereço digitado no checkout.
///
/// Só CEP, número e complemento chegam da tela. Logradouro, bairro, cidade e
/// estado vêm do ViaCEP: pedir que a pessoa digite bairro e cidade só cria chance
/// de ela escrever diferente do registro, e o erro de entrega aparece depois.
/// O <c>Logradouro</c> fica no pedido por causa de apartamento e bloco, em que o
/// ViaCEP devolve "de cima" e não sabe do número.
///
/// É o mesmo tipo de <c>RequisicaoDeEndereco</c> do cadastro de perfil, estendido
/// com os campos que o checkout aceita deixar em branco.
public sealed record RequisicaoDeEnderecoDoPedido(
    string Cep,
    string Numero,
    string? Complemento,
    string? Logradouro = null,
    string? Bairro = null,
    string? Cidade = null,
    string? Estado = null);

/// <summary>
/// Troca de endereço em um pedido que ainda não saiu.
///
/// Existe porque o carrinho guarda o preço do momento em que o item entrou, e o
/// pedido nasce assim que o checkout confirma. Se a pessoa lembrar do número errado
/// logo depois, a correção tem de ser possível sem refazer a compra — mas só
/// enquanto o pedido não saiu, porque o endereço é o registro do que foi entregue.
public sealed record RequisicaoDeAtualizacaoDeEnderecoDePedido(
    long PedidoId,
    RequisicaoDeEnderecoDoPedido Endereco);

public sealed record RequisicaoDeCriacaoDeEndereco(RequisicaoDeEnderecoDoPedido Endereco);

public sealed record ExclusaoDeEndereco(long EnderecoId);

/// <summary>
/// Cliente na lista de busca do painel. Não traz e-mail nem documento de pessoa
/// física em destaque porque a busca é por iniciais, e a tela mostra o bastante
/// para distinguir duas homônimas.
/// </summary>
public sealed record ClienteResumoDto(
    long Id,
    string Nome,
    string Email,
    string? Telefone,
    string? Cpf,
    bool TemSenha);

/// <summary>
/// Cadastro de cliente feito pela administradora, na venda de balcão. E-mail e
/// telefone são obrigatórios porque são por eles que o aviso de WhatsApp e o
/// e-mail de confirmação saem depois que o pedido existe.
/// </summary>
public sealed record RequisicaoDeCliente(
    string Nome,
    string Email,
    string Telefone,
    string? Cpf);

public sealed record RequisicaoDeCategoria(string Nome, string? Slug, string? Descricao, bool Ativo);

public sealed record RequisicaoDeMelhoriaDeTexto(string? Nome, string Descricao);

public sealed record RespostaDeMelhoriaDeTextoDto(string DescricaoMelhorada, string ModeloUsado);

/// <summary>
/// Produto na vitrine. Não leva <c>PrecoCusto</c>: custo é informação interna
/// e deixá-lo na API pública entregaria a margem de quem compra.
/// </summary>
public sealed record ProdutoDto(
    long Id,
    string Nome,
    string Slug,
    string Descricao,
    decimal PrecoVenda,
    decimal? PrecoPromocional,
    int QuantidadeEstoque,
    bool Ativo,
    bool Destaque,
    long CategoriaId,
    string NomeCategoria,
    IReadOnlyList<ImagemProdutoDto> Imagens);
