using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureVariantesInnoDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `variantes_produto` ENGINE = InnoDB;");

            migrationBuilder.Sql("ALTER TABLE `itens_pedido` DROP COLUMN IF EXISTS `VarianteProdutoId`;");

            migrationBuilder.AddColumn<long>(
                name: "VarianteProdutoId",
                table: "itens_pedido",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_itens_pedido_VarianteProdutoId",
                table: "itens_pedido",
                column: "VarianteProdutoId");

            migrationBuilder.AddForeignKey(
                name: "FK_itens_pedido_variantes_produto_VarianteProdutoId",
                table: "itens_pedido",
                column: "VarianteProdutoId",
                principalTable: "variantes_produto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_itens_pedido_variantes_produto_VarianteProdutoId",
                table: "itens_pedido");

            migrationBuilder.DropIndex(
                name: "IX_itens_pedido_VarianteProdutoId",
                table: "itens_pedido");

            migrationBuilder.DropColumn(
                name: "VarianteProdutoId",
                table: "itens_pedido");
        }
    }
}
