using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class EnderecoDeEntregaNoPedido : Migration
    {
        private const string CopiarEnderecoDoPedido = @"
UPDATE `pedidos` AS `p`
INNER JOIN `enderecos` AS `e` ON `e`.`Id` = `p`.`EnderecoEntregaId`
SET `p`.`EnderecoCep` = `e`.`Cep`,
    `p`.`EnderecoLogradouro` = `e`.`Logradouro`,
    `p`.`EnderecoNumero` = `e`.`Numero`,
    `p`.`EnderecoComplemento` = `e`.`Complemento`,
    `p`.`EnderecoBairro` = `e`.`Bairro`,
    `p`.`EnderecoCidade` = `e`.`Cidade`,
    `p`.`EnderecoEstado` = `e`.`Estado`;";

        private const string MarcarEnderecoDesconhecido = @"
UPDATE `pedidos`
SET `EnderecoCep` = '',
    `EnderecoLogradouro` = '(endereço não informado)',
    `EnderecoNumero` = '',
    `EnderecoBairro` = '(endereço não informado)',
    `EnderecoCidade` = '(endereço não informado)',
    `EnderecoEstado` = 'ZZ'
WHERE `EnderecoEstado` = '' AND `EnderecoLogradouro` = '';";

        private const string RecuperarEnderecoDoPedido = @"
UPDATE `pedidos` AS `p`
INNER JOIN `enderecos` AS `e`
    ON  `e`.`Cep` = `p`.`EnderecoCep`
    AND `e`.`Logradouro` = `p`.`EnderecoLogradouro`
    AND `e`.`Numero` = `p`.`EnderecoNumero`
    AND `e`.`Bairro` = `p`.`EnderecoBairro`
    AND `e`.`Cidade` = `p`.`EnderecoCidade`
    AND `e`.`Estado` = `p`.`EnderecoEstado`
SET `p`.`EnderecoEntregaId` = `e`.`Id`;";

        private const string RemoverVinculoComEndereco = @"
ALTER TABLE `pedidos`
    DROP FOREIGN KEY IF EXISTS `FK_pedidos_enderecos_EnderecoEntregaId`,
    DROP INDEX `IX_pedidos_EnderecoEntregaId`,
    DROP COLUMN `EnderecoEntregaId`;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As colunas entram com o mesmo tipo e tamanho de `enderecos`, para que a
            // cópia dos pedidos existentes não trunque nenhum valor. Já nascem NOT NULL
            // com string vazia, de modo que não é preciso alterar a coluna depois.
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

            // O destino de cada pedido só pode ser copiado enquanto a FK existe, por isso
            // o backfill vem antes de remover a coluna.
            migrationBuilder.Sql(CopiarEnderecoDoPedido);

            // Pedidos com EnderecoEntregaId zerado ou apontando para um endereço que não
            // existe mais não têm o que copiar. O marcador abaixo é aplicado a eles em vez
            // de deixar o endereço em branco, para que a informação ausente fique visível
            // e não passe por um endereço vazio legítimo.
            migrationBuilder.Sql(MarcarEnderecoDesconhecido);

            // Índice e coluna não podem ser removidos enquanto a FK existir: o MariaDB
            // recusa com "Cannot drop index ...: needed in a foreign key constraint". Como
            // o schema real do banco está sem qualquer FK, embora o modelo do EF defina
            // uma para pedidos -> enderecos, um DROP FOREIGN KEY incondicional abortaria
            // a migration com erro 3940. O IF EXISTS resolve os dois casos numa tacada só.
            // É sintaxe do MariaDB 10.11 e não existe no MySQL.
            migrationBuilder.Sql(RemoverVinculoComEndereco);
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

            // O Down é uma operação com perda: a relação original guardava apenas a FK,
            // então o vínculo só pode ser aproximado comparando os campos de endereço. A
            // coluna volta anulável porque nem todo endereço tem um registro correspondente
            // em `enderecos` — restaurada como NOT NULL zerado, o rollback falharia por
            // causa dos pedidos que ficariam com valor 0 apontando para o nada.
            migrationBuilder.AddColumn<long>(
                name: "EnderecoEntregaId",
                table: "pedidos",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(RecuperarEnderecoDoPedido);

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
