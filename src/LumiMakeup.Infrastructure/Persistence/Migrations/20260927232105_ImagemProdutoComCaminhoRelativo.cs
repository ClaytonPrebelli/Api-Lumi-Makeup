using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImagemProdutoComCaminhoRelativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UrlImagem",
                table: "imagens_produto",
                newName: "CaminhoRelativo");

            migrationBuilder.AddColumn<string>(
                name: "NomeOriginal",
                table: "imagens_produto",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NomeOriginal",
                table: "imagens_produto");

            migrationBuilder.RenameColumn(
                name: "CaminhoRelativo",
                table: "imagens_produto",
                newName: "UrlImagem");
        }
    }
}
