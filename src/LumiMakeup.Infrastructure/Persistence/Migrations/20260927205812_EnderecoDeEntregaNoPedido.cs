using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnderecoDeEntregaNoPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pedidos_enderecos_EnderecoEntregaId",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "IX_pedidos_EnderecoEntregaId",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EnderecoEntregaId",
                table: "pedidos");

            migrationBuilder.AddColumn<string>(
                name: "EnderecoBairro",
                table: "pedidos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EnderecoCep",
                table: "pedidos",
                type: "varchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EnderecoCidade",
                table: "pedidos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EnderecoComplemento",
                table: "pedidos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EnderecoEstado",
                table: "pedidos",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EnderecoLogradouro",
                table: "pedidos",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EnderecoNumero",
                table: "pedidos",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnderecoBairro",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EnderecoCep",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EnderecoCidade",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EnderecoComplemento",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EnderecoEstado",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EnderecoLogradouro",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EnderecoNumero",
                table: "pedidos");

            migrationBuilder.AddColumn<long>(
                name: "EnderecoEntregaId",
                table: "pedidos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_EnderecoEntregaId",
                table: "pedidos",
                column: "EnderecoEntregaId");

            migrationBuilder.AddForeignKey(
                name: "FK_pedidos_enderecos_EnderecoEntregaId",
                table: "pedidos",
                column: "EnderecoEntregaId",
                principalTable: "enderecos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
