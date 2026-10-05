using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations;

public sealed partial class RecompraManual : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "configuracao_recompra",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                Ativa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Assunto = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Mensagem = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                AtualizadoEm = table.Column<DateTime>(type: "datetime", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_configuracao_recompra", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4")
            .Annotation("MySql:Engine", "InnoDB");

        migrationBuilder.CreateTable(
            name: "envios_recompra",
            columns: table => new
            {
                PedidoId = table.Column<long>(type: "bigint", nullable: false),
                ReservadoEm = table.Column<DateTime>(type: "datetime", nullable: false),
                EnviadoEm = table.Column<DateTime>(type: "datetime", nullable: true),
                Versao = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_envios_recompra", x => x.PedidoId);
            })
            .Annotation("MySql:Engine", "InnoDB");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new InvalidOperationException("A migration de recompra é aditiva e não pode remover dados.");
    }
}
