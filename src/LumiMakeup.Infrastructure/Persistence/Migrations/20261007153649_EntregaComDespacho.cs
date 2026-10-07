using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntregaComDespacho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "usuarios",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(150)",
                oldMaxLength: 150)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "usuarios",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Login",
                table: "usuarios",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DespachadoEm",
                table: "pedidos",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntreguePorNome",
                table: "pedidos",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "EntreguePorUsuarioId",
                table: "pedidos",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Login",
                table: "usuarios",
                column: "Login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_usuarios_Login",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "Login",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "DespachadoEm",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EntreguePorNome",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "EntreguePorUsuarioId",
                table: "pedidos");

            migrationBuilder.UpdateData(
                table: "usuarios",
                keyColumn: "Email",
                keyValue: null,
                column: "Email",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "usuarios",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(150)",
                oldMaxLength: 150,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
