using LumiMakeup.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LumiMakeup.Infrastructure.Persistence.Configurations;

public class CupomConfiguration : IEntityTypeConfiguration<Cupom>
{
    public void Configure(EntityTypeBuilder<Cupom> builder)
    {
        builder.ToTable("cupons");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).HasMaxLength(40).IsRequired();
        builder.Property(c => c.Percentual).HasColumnType("decimal(5,2)");
        // Token de concorrencia: o UPDATE do EF inclui o valor lido no WHERE. Se
        // outra requisicao mexeu na quantidade no meio, o UPDATE afeta zero linhas
        // e o EF lanca DbUpdateConcurrencyException — o que barra dois clientes
        // com a ultima unidade sobrando de levarem os dois.
        //
        // Era um UPDATE guardado por "QuantidadeDisponivel > 0", mas esse jeito nao
        // roda no provedor InMemory e deixava a criacao de pedido com cupom sem
        // teste nenhum. Aqui a protecao e a mesma e o caminho continua testavel.
        builder.Property(c => c.QuantidadeDisponivel).IsConcurrencyToken();        builder.Property(c => c.ValorMinimo).HasColumnType("decimal(10,2)");
        builder.Property(c => c.ValidadeAte).HasColumnType("datetime");
        builder.Property(c => c.Ativo).HasColumnType("tinyint(1)");
        builder.Property(c => c.CriadoEm).HasColumnType("datetime(6)");

        // Unico porque dois cupons com o mesmo texto nao podem ter comportamentos
        // diferentes: o cliente digita um codigo, e nao sabe qual dos dois vale. O
        // codigo ja e gravado normalizado, sem acento e em maiuscula, para que
        // "NATAL20" e "natal20" sejam o mesmo cupom.
        builder.HasIndex(c => c.Codigo).IsUnique();

        // Sao estes dois campos que a vitrine consulta: os cupons ativos, com uso
        // restante e ainda dentro da validade.
        builder.HasIndex(c => c.Ativo);
    }
}
