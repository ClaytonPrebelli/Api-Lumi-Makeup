using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Application.Abstractions;

public interface ICatalogoService
{
    Task<IReadOnlyList<CategoriaDto>> ObterCategoriasAtivasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProdutoDto>> ObterProdutosAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>Produtos marcados para a vitrine da home, entre os ativos.</summary>
    Task<IReadOnlyList<ProdutoDto>> ObterProdutosDestaqueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Banners ativos e ordenados que abrem a home. Sai daqui, e nao do
    /// <see cref="IGestaoDeBannersService"/>, porque a vitrine publica nunca deve
    /// enxergar banner inativo nem os dados de administracao.
    /// </summary>
    Task<IReadOnlyList<BannerDto>> ObterBannersAtivosAsync(CancellationToken cancellationToken = default);

    Task<ProdutoDto?> ObterProdutoPorSlugAsync(string slug, CancellationToken cancellationToken = default);
}

public interface IGestaoDeProdutosService
{
    const int MaximoDeImagensPorProduto = 3;

    Task<IReadOnlyList<ProdutoAdministracaoDto>> ObterTodosAsync(CancellationToken cancellationToken = default);
    Task<ProdutoAdministracaoDto?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ProdutoAdministracaoDto> CriarAsync(RequisicaoDeProduto requisicao, CancellationToken cancellationToken = default);
    Task<ProdutoAdministracaoDto> AtualizarAsync(long id, RequisicaoDeProduto requisicao, CancellationToken cancellationToken = default);
    Task ExcluirAsync(long id, CancellationToken cancellationToken = default);
    Task<ImagemProdutoDto> AdicionarImagemAsync(long produtoId, Stream conteudo, string nomeOriginal, CancellationToken cancellationToken = default);
    Task ExcluirImagemAsync(long produtoId, long imagemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImagemProdutoDto>> ReordenarImagensAsync(long produtoId, IReadOnlyList<long> ids, CancellationToken cancellationToken = default);

    Task<ProdutoAdministracaoDto> SomarQuantidadeEstoqueAsync(long id, RequisicaoDeSomaDeQuantidadeDeProduto requisicao, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MovimentoEstoqueDto>> ObterMovimentosEstoqueAsync(long produtoId, CancellationToken cancellationToken = default);
    Task<MovimentoEstoqueDto> RegistrarMovimentoEstoqueAsync(long produtoId, RequisicaoDeMovimentoEstoque requisicao, long? usuarioId, CancellationToken cancellationToken = default);
}

public interface IGestaoDeCategoriasService
{
    Task<IReadOnlyList<CategoriaDto>> ObterTodasAsync(CancellationToken cancellationToken = default);
    Task<CategoriaDto> CriarAsync(RequisicaoDeCategoria requisicao, CancellationToken cancellationToken = default);
    Task<CategoriaDto> AtualizarAsync(long id, RequisicaoDeCategoria requisicao, CancellationToken cancellationToken = default);
    Task ExcluirAsync(long id, CancellationToken cancellationToken = default);
}

public interface IGestaoDeBannersService
{
    /// <summary>
    /// Quantidade de slides do carrossel. Limite rigido, e nao um aviso: o
    /// layout da home foi desenhado para tres, e um quarto slide empurra o
    /// restante para fora da dobra em telas de celular.
    /// </summary>
    const int MaximoDeBanners = 3;

    /// <summary>Todos os banners, ativos ou nao, ja ordenados. Uso do painel.</summary>
    Task<IReadOnlyList<BannerAdministracaoDto>> ObterTodosAsync(CancellationToken cancellationToken = default);

    /// <summary>Banners ativos e ordenados, que sao os que aparecem na vitrine.</summary>
    Task<IReadOnlyList<BannerDto>> ObterAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um slide com as duas imagens ja gravadas. Ordem e Ativo ficam de
    /// fora da requisicao de proposito: sao decididos pela reordenacao e pela
    /// ativacao, para nao misturar duas operacoes que mexem no mesmo par de
    /// campos.
    /// </summary>
    Task<BannerAdministracaoDto> CriarAsync(
        Stream imagemDesktop,
        string nomeOriginalDesktop,
        Stream imagemMobile,
        string nomeOriginalMobile,
        string? textoAlternativo,
        CancellationToken cancellationToken = default);

    Task<BannerAdministracaoDto> AtualizarAsync(
        long id,
        Stream? imagemDesktop,
        string? nomeOriginalDesktop,
        Stream? imagemMobile,
        string? nomeOriginalMobile,
        string? textoAlternativo,
        CancellationToken cancellationToken = default);

    Task<BannerAdministracaoDto> DefinirAtivoAsync(long id, bool ativo, CancellationToken cancellationToken = default);

    Task ExcluirAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BannerAdministracaoDto>> ReordenarAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);
}

public interface IGestaoDeCuponsService
{
    Task<IReadOnlyList<CupomDto>> ObterTodosAsync(CancellationToken cancellationToken = default);
    Task<CupomDto> CriarAsync(RequisicaoDeCupom requisicao, CancellationToken cancellationToken = default);
    Task<CupomDto> AtualizarAsync(long id, RequisicaoDeCupom requisicao, CancellationToken cancellationToken = default);
    Task<CupomDto> DefinirAtivoAsync(long id, bool ativo, CancellationToken cancellationToken = default);
    Task ExcluirAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soma unidades ao estoque de usos, sem reescrever o número. É a operação do
    /// dia a dia quando o cupom esgota, e é separada da edição porque o campo de
    /// quantidade na tela é um incremento, não um valor absoluto — reescrever o
    /// valor absoluto com duas pessoas na tela apagaria o uso da outra.
    /// </summary>
    Task<CupomDto> SomarQuantidadeAsync(long id, int quantidade, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confere o cupom contra o subtotal e devolve o desconto, ou lança com a
    /// mensagem que o cliente vai ler. Não altera nada: quem consome é o
    /// <see cref="ConsumirAsync"/>, chamado na transação do pedido.
    /// </summary>
    Task<AplicacaoDeCupom> CalcularAsync(
        string codigo,
        decimal subtotalDosProdutos,
        DateTime em,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consome uma unidade. Entra na transação que o chamador estiver usando — os
    /// dois serviços compartilham o mesmo <c>LumiDbContext</c> — para que a
    /// quantidade do cupom e a linha do pedido confirmem ou desfaçam juntas.
    /// </summary>
    Task ConsumirAsync(long cupomId, CancellationToken cancellationToken = default);

    /// <summary>Devolve uma unidade. Chamado no cancelamento do pedido.</summary>
    Task DevolverAsync(long cupomId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Busca e cadastro de cliente para o painel.
///
/// Separado do <see cref="IAutenticacaoService"/> porque o cadastro de balcão não
/// é o mesmo que o cadastro da loja: ele não passa por reCAPTCHA (a administradora
/// está autenticada, e o reCAPTCHA existe para bloquear robô, não ela), não gera
/// token e não devolve sessão. Misturar os dois faria o painel depender de reCAPTCHA
/// e de fluxo de token para registrar uma venda.
/// </summary>
/// <summary>
/// Agenda de endereços do cliente.
///
/// O endereço entra na agenda quando o cliente usa um endereço novo no checkout.
/// A regra está no roadmap desde o começo: o checkout nunca perde um endereço que
/// a pessoa digitou, porque ela teria que digitá-lo de novo na próxima compra.
///
/// O pedido guarda cópia própria do endereço, então apagar ou editar aqui nunca
/// altera um pedido antigo — ver <c>Pedido</c>.
/// </summary>
/// <summary>
/// Cálculo do frete pelo endereço de entrega.
///
/// Roda no checkout, depois que o endereço está escolhido — o carrinho mostra
/// "a calcular no checkout" porque sem endereço não há o que calcular. O valor
/// devolvido aqui é o que o servidor recalcula ao criar o pedido; a prévia
/// serve para mostrar, não para cobrar.
/// </summary>
public interface ICalculoDeFreteService
{
    Task<CalculoDeFreteDto> CalcularAsync(
        EnderecoDeEntregaRequisicao destino,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuração do frete pela administradora.
///
/// O cálculo de frete é recusado enquanto a loja não tiver uma linha aqui, e a
/// tela mostra o motivo. Editar a tarifa direto no banco seria possível, mas
/// ninguém lembraria depois que ela mudou.
/// </summary>
public interface IGestaoDeFreteService
{
    /// <summary>
    /// A configuração atual, ou uma vazia quando ainda não há nenhuma — que é o
    /// estado inicial de uma loja abrindo, e não um erro.
    /// </summary>
    Task<ConfiguracaoDeFreteDto> ObterAsync(CancellationToken cancellationToken = default);

    Task<ConfiguracaoDeFreteDto> SalvarAsync(
        RequisicaoDeConfiguracaoDeFrete requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Frete para um CEP, sem criar pedido. Serve para a tela mostrar o valor
    /// antes de a administradora gravar a tarifa, e usa o mesmo cálculo do
    /// checkout — o número da tela é o número que o cliente vai ver.
    /// </summary>
    Task<CalculoDeFreteDto> SimularAsync(string cep, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configura��ǜo da mensagem de WhatsApp da loja.
/// </summary>
public interface IGestaoDeWhatsAppService
{
    Task<ConfiguracaoWhatsAppDto> ObterAsync(CancellationToken cancellationToken = default);

    Task<ConfiguracaoWhatsAppDto> SalvarAsync(
        RequisicaoDeConfiguracaoWhatsApp requisicao,
        CancellationToken cancellationToken = default);

    Task<PreviaMensagemWhatsAppDto> GerarPreviaAsync(
        string? mensagemInicial,
        CancellationToken cancellationToken = default);
}

public interface IGestaoDeEnderecosService
{
    /// <summary>Agenda do cliente, com o padrão primeiro.</summary>
    Task<IReadOnlyList<EnderecoDto>> ListarDoUsuarioAsync(long usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cadastra um endereço. O primeiro da agenda vira o padrão automaticamente,
    /// porque é o que o checkout oferece por primeiro.
    /// </summary>
    Task<EnderecoDto> CriarAsync(
        long usuarioId,
        RequisicaoDeEnderecoDoPedido requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica a mudança de endereço ao pedido que ainda não saiu para entrega.
    ///
    /// O pedido tem cópia própria do endereço, e a cópia é o que vale. Trocar o
    /// endereço depois que o pedido saiu mudaria o registro do que foi entregue, e
    /// a pessoa não estaria mais no lugar que recebeu.
    /// </summary>
    Task<EnderecoDto> AtualizarNoPedidoAsync(
        long usuarioId,
        long pedidoId,
        RequisicaoDeEnderecoDoPedido requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>Apaga da agenda. Não toca em pedido nenhum, porque o pedido tem cópia.</summary>
    Task ExcluirAsync(long usuarioId, long enderecoId, CancellationToken cancellationToken = default);
}

public interface IGestaoDeClientesService
{
    /// <summary>
    /// Procura por nome, e-mail, telefone ou CPF. Vazio traz os mais recentes,
    /// que é o que a tela mostra antes de a administradora digitar qualquer coisa.
    /// </summary>
    Task<IReadOnlyList<ClienteResumoDto>> BuscarAsync(string? termo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cadastra um cliente sem senha. Sem senha é o caso normal da venda de balcão:
    /// a pessoa compra, e só vai ter senha se algum dia se cadastrar no site.
    /// Criar senha aqui seria inventar uma credencial que ninguém escolheu.
    /// </summary>
    Task<ClienteResumoDto> CriarAsync(RequisicaoDeCliente requisicao, CancellationToken cancellationToken = default);
}

public interface IGestaoDePedidosService
{
    /// <summary>
    /// Cria o pedido, baixa o estoque e consome o cupom **na mesma transação**, e
    /// só depois avisa o cliente e a administradora.
    ///
    /// A ordem entre essas duas coisas é o ponto: se o aviso saísse antes da
    /// gravação e a gravação falhasse, o cliente teria recebido a confirmação de um
    /// pedido que não existe.
    /// </summary>
    Task<PedidoDto> CriarAsync(
        RequisicaoDePedido requisicao,
        OrigemPedido origem,
        CancellationToken cancellationToken = default);

    Task<PedidoDto?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Pedidos do cliente do token, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<PedidoDto>> ListarDoUsuarioAsync(long usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Listagem do painel. Os filtros são opcionais: sem nenhum, traz todos.
    ///
    /// <paramref name="notaFiscalGerada"/> é o valor da flag, e não "sem nota": com
    /// nome invertido, quem passasse <c>true</c> para "só os pendentes" receberia os
    /// já emitidos, que é o resultado oposto do pedido.
    /// </summary>
    Task<IReadOnlyList<PedidoListaDto>> ListarAsync(
        StatusPedido? status = null,
        OrigemPedido? origem = null,
        bool? notaFiscalGerada = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A administradora registra a forma de pagamento e aceita a venda. É o
    /// "finalizar" do fluxo: o pedido sai de <c>AguardandoPagamento</c> para
    /// <c>Pago</c>.
    ///
    /// Não há verificação de compensação bancária. Quem confirma o recebimento é a
    /// administradora, e uma checagem automática aqui só criaria um segundo critério
    /// disputando com o dela.
    /// </summary>
    Task<PedidoDto> RegistrarPagamentoAsync(
        long id,
        MetodoPagamento metodoPagamento,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela o pedido, devolvendo o estoque e a unidade de cupom consumida.
    /// Não há o que devolver em venda de balcão sem cupom, e por isso a devolução
    /// é condicional.
    /// </summary>
    Task<PedidoDto> CancelarAsync(long id, CancellationToken cancellationToken = default);
}
