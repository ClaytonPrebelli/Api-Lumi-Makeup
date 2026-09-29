using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NucleoDePedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "UsuarioId",
                table: "pedidos",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoNumero",
                table: "pedidos",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoLogradouro",
                table: "pedidos",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoEstado",
                table: "pedidos",
                type: "varchar(2)",
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldMaxLength: 2)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoCidade",
                table: "pedidos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoCep",
                table: "pedidos",
                type: "varchar(9)",
                maxLength: 9,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(9)",
                oldMaxLength: 9)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoBairro",
                table: "pedidos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CupomCodigo",
                table: "pedidos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "Desconto",
                table: "pedidos",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoCliente",
                table: "pedidos",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NomeCliente",
                table: "pedidos",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "pedidos",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoPromocionalUnitario",
                table: "itens_pedido",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_CriadoEm",
                table: "pedidos",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_Origem",
                table: "pedidos",
                column: "Origem");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_Status",
                table: "pedidos",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pedidos_CriadoEm",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "IX_pedidos_Origem",
                table: "pedidos");

            migrationBuilder.DropIndex(
                name: "IX_pedidos_Status",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "CupomCodigo",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "Desconto",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "DocumentoCliente",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "NomeCliente",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "PrecoPromocionalUnitario",
                table: "itens_pedido");

            migrationBuilder.AlterColumn<long>(
                name: "UsuarioId",
                table: "pedidos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "EnderecoNumero",
                keyValue: null,
                column: "EnderecoNumero",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoNumero",
                table: "pedidos",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "EnderecoLogradouro",
                keyValue: null,
                column: "EnderecoLogradouro",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoLogradouro",
                table: "pedidos",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "EnderecoEstado",
                keyValue: null,
                column: "EnderecoEstado",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoEstado",
                table: "pedidos",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(2)",
                oldMaxLength: 2,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "EnderecoCidade",
                keyValue: null,
                column: "EnderecoCidade",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoCidade",
                table: "pedidos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "EnderecoCep",
                keyValue: null,
                column: "EnderecoCep",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoCep",
                table: "pedidos",
                type: "varchar(9)",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(9)",
                oldMaxLength: 9,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "pedidos",
                keyColumn: "EnderecoBairro",
                keyValue: null,
                column: "EnderecoBairro",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoBairro",
                table: "pedidos",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
