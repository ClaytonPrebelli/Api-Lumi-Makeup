using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class RestaurarIntegridadeReferencial : Migration
    {
        /// <summary>
        /// Todas as tabelas do banco, inclusive a de histórico do EF. InnoDB é o motor
        /// que o Pomelo assume por padrão, então converte-las é alinhar o banco com o
        /// que o modelo já espera.
        /// </summary>
        private static readonly string[] Tabelas =
        {
            "categorias",
            "configuracao_frete",
            "despesas",
            "enderecos",
            "imagens_produto",
            "itens_pedido",
            "notas_fiscais",
            "pedidos",
            "produtos",
            "recuperacoes_de_senha",
            "registros_whatsapp",
            "usuarios",
            "__efmigrationshistory",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O servidor está com default_storage_engine = MyISAM, e é por isso que
            // nenhuma das 10 chaves estrangeiras que o modelo declara chegou a existir:
            // MyISAM ignora a clause CONSTRAINT na criação da tabela, sem avisar. Por
            // consequência, a integridade referencial nunca foi garantida pelo banco, e
            // nenhuma transação funcionou — o START TRANSACTION/COMMIT que o EF emite
            // ao aplicar migrations é um no-op em MyISAM, e o SaveChanges do EF não tem
            // atomicidade. InnoDB é o que dá as duas garantias.
            foreach (var tabela in Tabelas)
            {
                migrationBuilder.Sql($"ALTER TABLE `{tabela}` ENGINE = InnoDB;");
            }

            // Criadas depois da conversão, porque MyISAM as ignoraria.
            // Nome e delete behavior seguem exatamente o que o modelo declara.
            migrationBuilder.Sql(
                "ALTER TABLE `despesas` ADD CONSTRAINT `FK_despesas_usuarios_CriadoPor` " +
                "FOREIGN KEY (`CriadoPor`) REFERENCES `usuarios` (`Id`) ON DELETE RESTRICT ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `enderecos` ADD CONSTRAINT `FK_enderecos_usuarios_UsuarioId` " +
                "FOREIGN KEY (`UsuarioId`) REFERENCES `usuarios` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `imagens_produto` ADD CONSTRAINT `FK_imagens_produto_produtos_ProdutoId` " +
                "FOREIGN KEY (`ProdutoId`) REFERENCES `produtos` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `itens_pedido` ADD CONSTRAINT `FK_itens_pedido_pedidos_PedidoId` " +
                "FOREIGN KEY (`PedidoId`) REFERENCES `pedidos` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `itens_pedido` ADD CONSTRAINT `FK_itens_pedido_produtos_ProdutoId` " +
                "FOREIGN KEY (`ProdutoId`) REFERENCES `produtos` (`Id`) ON DELETE RESTRICT ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `notas_fiscais` ADD CONSTRAINT `FK_notas_fiscais_pedidos_PedidoId` " +
                "FOREIGN KEY (`PedidoId`) REFERENCES `pedidos` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `pedidos` ADD CONSTRAINT `FK_pedidos_usuarios_UsuarioId` " +
                "FOREIGN KEY (`UsuarioId`) REFERENCES `usuarios` (`Id`) ON DELETE RESTRICT ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `produtos` ADD CONSTRAINT `FK_produtos_categorias_CategoriaId` " +
                "FOREIGN KEY (`CategoriaId`) REFERENCES `categorias` (`Id`) ON DELETE RESTRICT ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `recuperacoes_de_senha` ADD CONSTRAINT `FK_recuperacoes_de_senha_usuarios_UsuarioId` " +
                "FOREIGN KEY (`UsuarioId`) REFERENCES `usuarios` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE;");

            migrationBuilder.Sql(
                "ALTER TABLE `registros_whatsapp` ADD CONSTRAINT `FK_registros_whatsapp_pedidos_PedidoId` " +
                "FOREIGN KEY (`PedidoId`) REFERENCES `pedidos` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `registros_whatsapp` DROP FOREIGN KEY `FK_registros_whatsapp_pedidos_PedidoId`;");
            migrationBuilder.Sql("ALTER TABLE `recuperacoes_de_senha` DROP FOREIGN KEY `FK_recuperacoes_de_senha_usuarios_UsuarioId`;");
            migrationBuilder.Sql("ALTER TABLE `produtos` DROP FOREIGN KEY `FK_produtos_categorias_CategoriaId`;");
            migrationBuilder.Sql("ALTER TABLE `pedidos` DROP FOREIGN KEY `FK_pedidos_usuarios_UsuarioId`;");
            migrationBuilder.Sql("ALTER TABLE `notas_fiscais` DROP FOREIGN KEY `FK_notas_fiscais_pedidos_PedidoId`;");
            migrationBuilder.Sql("ALTER TABLE `itens_pedido` DROP FOREIGN KEY `FK_itens_pedido_produtos_ProdutoId`;");
            migrationBuilder.Sql("ALTER TABLE `itens_pedido` DROP FOREIGN KEY `FK_itens_pedido_pedidos_PedidoId`;");
            migrationBuilder.Sql("ALTER TABLE `imagens_produto` DROP FOREIGN KEY `FK_imagens_produto_produtos_ProdutoId`;");
            migrationBuilder.Sql("ALTER TABLE `enderecos` DROP FOREIGN KEY `FK_enderecos_usuarios_UsuarioId`;");
            migrationBuilder.Sql("ALTER TABLE `despesas` DROP FOREIGN KEY `FK_despesas_usuarios_CriadoPor`;");

            // Voltar para MyISAM é uma regressão, e não uma reversão honesta: as
            // constraints voltariam a ser ignoradas e as transações junto com elas.
            // Está aqui apenas para que o Down restaure o estado anterior do banco.
            foreach (var tabela in Tabelas)
            {
                migrationBuilder.Sql($"ALTER TABLE `{tabela}` ENGINE = MyISAM;");
            }
        }
    }
}
