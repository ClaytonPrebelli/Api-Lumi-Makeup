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
