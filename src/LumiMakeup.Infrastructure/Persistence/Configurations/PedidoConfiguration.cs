using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.StatusEntrega)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.MetodoPagamento)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(o => o.Origem)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.NomeCliente).HasMaxLength(150).IsRequired();
        builder.Property(o => o.DocumentoCliente).HasMaxLength(20);
        builder.Property(o => o.TelefoneContato).HasMaxLength(20);
        builder.Property(o => o.EmailContato).HasMaxLength(200);

        // Endereco e obrigatorio em pedido online e nulo em venda de balcao. A
        // obrigatoriedade fica na criacao do pedido, e nao em constraint: ela
        // depende de outro campo (Origem), e um CHECK sobre dois campos nao daria
        // uma mensagem que o cliente entende.
        builder.Property(o => o.EnderecoCep).HasMaxLength(9);
        builder.Property(o => o.EnderecoLogradouro).HasMaxLength(200);
        builder.Property(o => o.EnderecoNumero).HasMaxLength(20);
        builder.Property(o => o.EnderecoComplemento).HasMaxLength(100);
        builder.Property(o => o.EnderecoBairro).HasMaxLength(100);
        builder.Property(o => o.EnderecoCidade).HasMaxLength(100);
        builder.Property(o => o.EnderecoEstado).HasMaxLength(2);

        builder.Property(o => o.DistanciaKm)
            .HasColumnName("distancekm")
            .HasColumnType("decimal(8,2)");
        builder.Property(o => o.CustoFrete).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Subtotal).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Desconto).HasColumnType("decimal(10,2)");
        builder.Property(o => o.CupomCodigo).HasMaxLength(40);
        builder.Property(o => o.Total).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Observacoes).HasColumnType("text");
        builder.Property(o => o.CriadoEm).HasColumnType("datetime");
        builder.Property(o => o.PagoEm).HasColumnType("datetime");
        builder.Property(o => o.EntregueEm).HasColumnType("datetime");

        // A origem e o que separa a venda de balcao no relatorio de receita.
        builder.HasIndex(o => o.Origem);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CriadoEm);

        // E o indice que o painel usa para achar o que ainda precisa de nota
        // fiscal. Composto com o status, porque so faz sentido listar pendentes de
        // pedido que chegou a ser pago.
        builder.HasIndex(o => new { o.NotaFiscalGerada, o.Status });

        builder.HasOne(o => o.Usuario)
            .WithMany(u => u.Pedidos)
            .HasForeignKey(o => o.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Itens)
            .WithOne(i => i.Pedido)
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.NotasFiscais)
            .WithOne(i => i.Pedido)
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.RegistrosWhatsApp)
            .WithOne(l => l.Pedido)
            .HasForeignKey(l => l.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}