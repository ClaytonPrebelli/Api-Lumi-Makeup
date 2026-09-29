using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotaFiscalGeradaNoPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotaFiscalGerada",
                table: "pedidos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotaFiscalGeradaEm",
                table: "pedidos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_NotaFiscalGerada_Status",
                table: "pedidos",
                columns: new[] { "NotaFiscalGerada", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pedidos_NotaFiscalGerada_Status",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "NotaFiscalGerada",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "NotaFiscalGeradaEm",
                table: "pedidos");
        }
    }
}
