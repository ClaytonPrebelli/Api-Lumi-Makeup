using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class Pedido
{
    public long Id { get; set; }

    /// <summary>
    /// Todo pedido tem cliente. Na venda de balcão não existe cliente sem conta:
    /// a admin cadastra a pessoa primeiro, como quem registra um cliente novo, e
    /// só depois registra a venda. Sem isso o pedido não teria telefone para o
    /// WhatsApp — que é justamente o canal por onde a venda se conclui.
    /// </summary>
    public long UsuarioId { get; set; }

    /// <summary>
    /// Nome do cliente no momento da compra. Vai aqui, e não como referência ao
    /// cadastro, pelo mesmo motivo do endereço: o pedido guarda cópia própria do
    /// que foi comprado. Cliente trocando de nome, ou cadastro apagado, não pode
    /// reescrever o passado.
    /// </summary>
    public string NomeCliente { get; set; } = string.Empty;

    /// <summary>CPF do cliente no momento da compra. Nulo só se o cadastro não tiver.</summary>
    public string? DocumentoCliente { get; set; }

    public OrigemPedido Origem { get; set; } = OrigemPedido.Online;

    // O endereço é obrigatório em pedido online e nulo em venda de balcão, em que
    // o cliente não sai da loja. A regra é validada na criação do pedido, e não
    // pelo banco: uma constraint que depende de outro campo é checável em código e
    // legível na mensagem que o cliente recebe.
    public string? EnderecoCep { get; set; }
    public string? EnderecoLogradouro { get; set; }
    public string? EnderecoNumero { get; set; }
    public string? EnderecoComplemento { get; set; }
    public string? EnderecoBairro { get; set; }
    public string? EnderecoCidade { get; set; }
    public string? EnderecoEstado { get; set; }

    /// <summary>
    /// Estado inicial. O pedido nasce aguardando pagamento e fica assim até a
    /// admin registrar a forma de pagamento e aceitar a venda, ou cancelar.
    /// Não há verificação de compensação bancária: quem dá o aceite é a admin.
    /// </summary>
    public StatusPedido Status { get; set; } = StatusPedido.AguardandoPagamento;

    public StatusEntrega StatusEntrega { get; set; } = StatusEntrega.NaoEnviado;

    /// <summary>
    /// Preenchido pela admin, nunca pelo cliente. Fica nulo enquanto o pedido
    /// aguarda, e é a última informação que falta para o pedido virar pago.
    /// </summary>
    public MetodoPagamento? MetodoPagamento { get; set; }

    /// <summary>
    /// Foto do comprovante de pagamento, anexada pela admin ao dar pago.
    /// Caminho relativo dentro da pasta de comprovantes, nas mesmas regras das
    /// imagens de produto e banner. Nulo quando nenhum comprovante foi anexado.
    /// </summary>
    public string? CaminhoComprovante { get; set; }

    /// <summary>Nome original do arquivo do comprovante, para exibição.</summary>
    public string? NomeOriginalComprovante { get; set; }

    public decimal DistanciaKm { get; set; }
    public decimal CustoFrete { get; set; }

    /// <summary>Soma dos itens, já com o preço promocional quando houver.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Desconto do cupom, aplicado só sobre <see cref="Subtotal"/>.</summary>
    public decimal Desconto { get; set; }

    /// <summary>Código do cupom usado, para o relatório e para auditoria.</summary>
    public string? CupomCodigo { get; set; }

    /// <summary><c>Subtotal − Desconto + Frete</c>. Nunca <c>(Subtotal + Frete) × p</c>.</summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Telefone usado para a mensagem de WhatsApp, copiado do cadastro no momento
    /// do pedido. Precisa ser cópia: o aviso vai para o cliente depois que o
    /// pedido existe, e o número do cadastro pode ter mudado nesse meio tempo.
    /// </summary>
    public string? TelefoneContato { get; set; }

    /// <summary>
    /// E-mail do cliente no momento da compra, pela mesma razão do telefone: é por
    /// ele que a administradora responde, e o aviso sai depois que o pedido existe.
    /// </summary>
    public string? EmailContato { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>
    /// Se a nota fiscal deste pedido já foi gerada.
    ///
    /// Fica no pedido, e não só na coleção de notas, porque a pergunta que o painel
    /// faz é "quais pedidos estão sem nota?" — e responder isso varrendo as notas
    /// de cada pedido seria trocar uma coluna por uma consulta em cada linha da
    /// listagem.
    ///
    /// Começa falso e vira verdadeiro quando a emissão dá certo. Enquanto a emissão
    /// não existir (o Focus NFe ainda é stub), todo pedido nasce aqui e o painel
    /// mostra a fila inteira como pendente, que é a leitura correta.
    /// </summary>
    public bool NotaFiscalGerada { get; set; }

    /// <summary>Quando a nota foi gerada. Nulo enquanto pendente.</summary>
    public DateTime? NotaFiscalGeradaEm { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? PagoEm { get; set; }
    public DateTime? EntregueEm { get; set; }

    /// <summary>
    /// Quando entrou no despacho do dia. Nulo enquanto está a despachar.
    /// </summary>
    public DateTime? DespachadoEm { get; set; }

    /// <summary>
    /// Quem marcou como entregue (id do entregador ou da admin). Sem FK, de
    /// propósito: é registro histórico, como o nome do cliente — desativar ou
    /// apagar o entregador não pode reescrever o passado.
    /// </summary>
    public long? EntreguePorUsuarioId { get; set; }

    /// <summary>Nome de quem entregou, copiado no momento da entrega.</summary>
    public string? EntreguePorNome { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public ICollection<ItemPedido> Itens { get; set; } = new List<ItemPedido>();
    public ICollection<NotaFiscal> NotasFiscais { get; set; } = new List<NotaFiscal>();
    public ICollection<RegistroWhatsApp> RegistrosWhatsApp { get; set; } = new List<RegistroWhatsApp>();
}