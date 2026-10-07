using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FretePorFaixas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TaxaMinima",
                table: "configuracao_frete",
                newName: "ValorAte8Km");

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAte16Km",
                table: "configuracao_frete",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAte25Km",
                table: "configuracao_frete",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValorAte16Km",
                table: "configuracao_frete");

            migrationBuilder.DropColumn(
                name: "ValorAte25Km",
                table: "configuracao_frete");

            migrationBuilder.RenameColumn(
                name: "ValorAte8Km",
                table: "configuracao_frete",
                newName: "TaxaMinima");
        }
    }
}
