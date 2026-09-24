using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Tests.Domain;

public class EnumsTests
{
    [Fact]
    public void PapelUsuario_expoe_membros_esperados()
    {
        Assert.Equal(0, (int)PapelUsuario.Cliente);
        Assert.Equal(1, (int)PapelUsuario.Administrador);
    }

    [Fact]
    public void StatusPedido_expoe_membros_esperados()
    {
        Assert.Equal(0, (int)StatusPedido.AguardandoPagamento);
        Assert.Equal(1, (int)StatusPedido.Pago);
        Assert.Equal(2, (int)StatusPedido.Cancelado);
    }

    [Fact]
    public void StatusEntrega_expoe_membros_esperados()
    {
        Assert.Equal(0, (int)StatusEntrega.NaoEnviado);
        Assert.Equal(1, (int)StatusEntrega.Enviado);
        Assert.Equal(2, (int)StatusEntrega.Entregue);
    }

    [Fact]
    public void MetodoPagamento_expoe_membros_esperados()
    {
        Assert.Equal(0, (int)MetodoPagamento.Pix);
        Assert.Equal(1, (int)MetodoPagamento.Cartao);
        Assert.Equal(2, (int)MetodoPagamento.Dinheiro);
        Assert.Equal(3, (int)MetodoPagamento.Outro);
    }

    [Fact]
    public void StatusNotaFiscal_expoe_membros_esperados()
    {
        Assert.Equal(0, (int)StatusNotaFiscal.Pendente);
        Assert.Equal(1, (int)StatusNotaFiscal.Emitida);
        Assert.Equal(2, (int)StatusNotaFiscal.Erro);
    }

    [Fact]
    public void StatusRegistroWhatsApp_expoe_membros_esperados()
    {
        Assert.Equal(0, (int)StatusRegistroWhatsApp.Enviado);
        Assert.Equal(1, (int)StatusRegistroWhatsApp.Falhou);
    }
}