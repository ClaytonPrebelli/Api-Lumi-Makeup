using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumiMakeup.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class RenomearParaPortugues : Migration
    {
        private const string RenameTable = "RENAME TABLE `{0}` TO `{1}`";
        private const string RenameColumn = "ALTER TABLE `{0}` RENAME COLUMN `{1}` TO `{2}`";
        private const string RenameIndex = "ALTER TABLE `{0}` RENAME INDEX `{1}` TO `{2}`";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tabelas
            migrationBuilder.Sql(string.Format(RenameTable, "users", "usuarios"));
            migrationBuilder.Sql(string.Format(RenameTable, "addresses", "enderecos"));
            migrationBuilder.Sql(string.Format(RenameTable, "categories", "categorias"));
            migrationBuilder.Sql(string.Format(RenameTable, "products", "produtos"));
            migrationBuilder.Sql(string.Format(RenameTable, "product_images", "imagens_produto"));
            migrationBuilder.Sql(string.Format(RenameTable, "shipping_config", "configuracao_frete"));
            migrationBuilder.Sql(string.Format(RenameTable, "orders", "pedidos"));
            migrationBuilder.Sql(string.Format(RenameTable, "order_items", "itens_pedido"));
            migrationBuilder.Sql(string.Format(RenameTable, "expenses", "despesas"));
            migrationBuilder.Sql(string.Format(RenameTable, "invoices", "notas_fiscais"));
            migrationBuilder.Sql(string.Format(RenameTable, "whatsapp_logs", "registros_whatsapp"));

            // Colunas — usuarios
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "Name", "Nome"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "PasswordHash", "HashSenha"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "GoogleId", "IdGoogle"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "Phone", "Telefone"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "Role", "Papel"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "CreatedAt", "CriadoEm"));

            // Colunas — enderecos
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "UserId", "UsuarioId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Street", "Logradouro"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Number", "Numero"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Complement", "Complemento"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Neighborhood", "Bairro"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "City", "Cidade"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "State", "Estado"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "IsDefault", "Padrao"));

            // Colunas — categorias
            migrationBuilder.Sql(string.Format(RenameColumn, "categorias", "Name", "Nome"));
            migrationBuilder.Sql(string.Format(RenameColumn, "categorias", "Description", "Descricao"));
            migrationBuilder.Sql(string.Format(RenameColumn, "categorias", "IsActive", "Ativo"));

            // Colunas — produtos
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "CategoryId", "CategoriaId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "Name", "Nome"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "Description", "Descricao"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "CostPrice", "PrecoCusto"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "SalePrice", "PrecoVenda"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "StockQuantity", "QuantidadeEstoque"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "IsActive", "Ativo"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "CreatedAt", "CriadoEm"));

            // Colunas — imagens_produto
            migrationBuilder.Sql(string.Format(RenameColumn, "imagens_produto", "ProductId", "ProdutoId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "imagens_produto", "ImageUrl", "UrlImagem"));
            migrationBuilder.Sql(string.Format(RenameColumn, "imagens_produto", "SortOrder", "Ordem"));

            // Colunas — pedidos
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "UserId", "UsuarioId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "ShippingAddressId", "EnderecoEntregaId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "DeliveryStatus", "StatusEntrega"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "ShippingCost", "CustoFrete"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "Notes", "Observacoes"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "CreatedAt", "CriadoEm"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "PaidAt", "PagoEm"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "DeliveredAt", "EntregueEm"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "PaymentMethod", "MetodoPagamento"));

            // Colunas — itens_pedido
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "OrderId", "PedidoId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "ProductId", "ProdutoId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "ProductNameSnapshot", "NomeProdutoRegistrado"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "UnitCostPrice", "PrecoCustoUnitario"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "UnitSalePrice", "PrecoVendaUnitario"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "Quantity", "Quantidade"));

            // Colunas — despesas
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "Description", "Descricao"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "Category", "Categoria"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "Amount", "Valor"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "ExpenseDate", "DataDaDespesa"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "CreatedBy", "CriadoPor"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "CreatedAt", "CriadoEm"));

            // Colunas — notas_fiscais
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "OrderId", "PedidoId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "FocusNfeReference", "ReferenciaFocusNfe"));
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "XmlUrl", "UrlXml"));
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "PdfUrl", "UrlPdf"));
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "IssuedAt", "EmitidaEm"));

            // Colunas — configuracao_frete
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "OriginCep", "CepOrigem"));
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "OriginLatitude", "LatitudeOrigem"));
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "OriginLongitude", "LongitudeOrigem"));
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "PricePerKm", "PrecoPorKm"));
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "MinimumFee", "TaxaMinima"));

            // Colunas — registros_whatsapp
            migrationBuilder.Sql(string.Format(RenameColumn, "registros_whatsapp", "OrderId", "PedidoId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "registros_whatsapp", "PhoneNumber", "Telefone"));
            migrationBuilder.Sql(string.Format(RenameColumn, "registros_whatsapp", "Message", "Mensagem"));
            migrationBuilder.Sql(string.Format(RenameColumn, "registros_whatsapp", "SentAt", "EnviadoEm"));

            // Valores de papel dos usuários
            migrationBuilder.Sql("UPDATE `usuarios` SET `Papel` = 'Administrador' WHERE `Papel` = 'Admin';");
            migrationBuilder.Sql("UPDATE `usuarios` SET `Papel` = 'Cliente' WHERE `Papel` = 'Customer';");

            // Índices
            migrationBuilder.Sql(string.Format(RenameIndex, "usuarios", "IX_users_Email", "IX_usuarios_Email"));
            migrationBuilder.Sql(string.Format(RenameIndex, "usuarios", "IX_users_GoogleId", "IX_usuarios_IdGoogle"));
            migrationBuilder.Sql(string.Format(RenameIndex, "usuarios", "IX_users_Cpf", "IX_usuarios_Cpf"));
            migrationBuilder.Sql(string.Format(RenameIndex, "enderecos", "IX_addresses_UserId", "IX_enderecos_UsuarioId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "categorias", "IX_categories_Slug", "IX_categorias_Slug"));
            migrationBuilder.Sql(string.Format(RenameIndex, "produtos", "IX_products_CategoryId", "IX_produtos_CategoriaId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "produtos", "IX_products_Slug", "IX_produtos_Slug"));
            migrationBuilder.Sql(string.Format(RenameIndex, "imagens_produto", "IX_product_images_ProductId", "IX_imagens_produto_ProdutoId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "pedidos", "IX_orders_UserId", "IX_pedidos_UsuarioId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "pedidos", "IX_orders_ShippingAddressId", "IX_pedidos_EnderecoEntregaId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "itens_pedido", "IX_order_items_OrderId", "IX_itens_pedido_PedidoId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "itens_pedido", "IX_order_items_ProductId", "IX_itens_pedido_ProdutoId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "despesas", "IX_expenses_CreatedBy", "IX_despesas_CriadoPor"));
            migrationBuilder.Sql(string.Format(RenameIndex, "notas_fiscais", "IX_invoices_OrderId", "IX_notas_fiscais_PedidoId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "registros_whatsapp", "IX_whatsapp_logs_OrderId", "IX_registros_whatsapp_PedidoId"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Índices (reverter)
            migrationBuilder.Sql(string.Format(RenameIndex, "registros_whatsapp", "IX_registros_whatsapp_PedidoId", "IX_whatsapp_logs_OrderId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "notas_fiscais", "IX_notas_fiscais_PedidoId", "IX_invoices_OrderId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "despesas", "IX_despesas_CriadoPor", "IX_expenses_CreatedBy"));
            migrationBuilder.Sql(string.Format(RenameIndex, "itens_pedido", "IX_itens_pedido_ProdutoId", "IX_order_items_ProductId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "itens_pedido", "IX_itens_pedido_PedidoId", "IX_order_items_OrderId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "pedidos", "IX_pedidos_EnderecoEntregaId", "IX_orders_ShippingAddressId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "pedidos", "IX_pedidos_UsuarioId", "IX_orders_UserId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "imagens_produto", "IX_imagens_produto_ProdutoId", "IX_product_images_ProductId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "produtos", "IX_produtos_Slug", "IX_products_Slug"));
            migrationBuilder.Sql(string.Format(RenameIndex, "produtos", "IX_produtos_CategoriaId", "IX_products_CategoryId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "categorias", "IX_categorias_Slug", "IX_categories_Slug"));
            migrationBuilder.Sql(string.Format(RenameIndex, "enderecos", "IX_enderecos_UsuarioId", "IX_addresses_UserId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "usuarios", "IX_usuarios_Cpf", "IX_users_Cpf"));
            migrationBuilder.Sql(string.Format(RenameIndex, "usuarios", "IX_usuarios_IdGoogle", "IX_users_GoogleId"));
            migrationBuilder.Sql(string.Format(RenameIndex, "usuarios", "IX_usuarios_Email", "IX_users_Email"));

            // Valores de papel dos usuários
            migrationBuilder.Sql("UPDATE `usuarios` SET `Papel` = 'Admin' WHERE `Papel` = 'Administrador';");
            migrationBuilder.Sql("UPDATE `usuarios` SET `Papel` = 'Customer' WHERE `Papel` = 'Cliente';");

            // Colunas — registros_whatsapp
            migrationBuilder.Sql(string.Format(RenameColumn, "registros_whatsapp", "EnviadoEm", "SentAt"));
            migrationBuilder.Sql(string.Format(RenameColumn, "registros_whatsapp", "Mensagem", "Message"));
            migrationBuilder.Sql(string.Format(RenameColumn, "registros_whatsapp", "Telefone", "PhoneNumber"));
            migrationBuilder.Sql(string.Format(RenameColumn, "registros_whatsapp", "PedidoId", "OrderId"));

            // Colunas — configuracao_frete
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "TaxaMinima", "MinimumFee"));
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "PrecoPorKm", "PricePerKm"));
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "LongitudeOrigem", "OriginLongitude"));
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "LatitudeOrigem", "OriginLatitude"));
            migrationBuilder.Sql(string.Format(RenameColumn, "configuracao_frete", "CepOrigem", "OriginCep"));

            // Colunas — notas_fiscais
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "EmitidaEm", "IssuedAt"));
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "UrlPdf", "PdfUrl"));
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "UrlXml", "XmlUrl"));
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "ReferenciaFocusNfe", "FocusNfeReference"));
            migrationBuilder.Sql(string.Format(RenameColumn, "notas_fiscais", "PedidoId", "OrderId"));

            // Colunas — despesas
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "CriadoEm", "CreatedAt"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "CriadoPor", "CreatedBy"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "DataDaDespesa", "ExpenseDate"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "Valor", "Amount"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "Categoria", "Category"));
            migrationBuilder.Sql(string.Format(RenameColumn, "despesas", "Descricao", "Description"));

            // Colunas — itens_pedido
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "Quantidade", "Quantity"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "PrecoVendaUnitario", "UnitSalePrice"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "PrecoCustoUnitario", "UnitCostPrice"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "NomeProdutoRegistrado", "ProductNameSnapshot"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "ProdutoId", "ProductId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "itens_pedido", "PedidoId", "OrderId"));

            // Colunas — pedidos
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "MetodoPagamento", "PaymentMethod"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "EntregueEm", "DeliveredAt"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "PagoEm", "PaidAt"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "CriadoEm", "CreatedAt"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "Observacoes", "Notes"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "CustoFrete", "ShippingCost"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "StatusEntrega", "DeliveryStatus"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "EnderecoEntregaId", "ShippingAddressId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "pedidos", "UsuarioId", "UserId"));

            // Colunas — imagens_produto
            migrationBuilder.Sql(string.Format(RenameColumn, "imagens_produto", "Ordem", "SortOrder"));
            migrationBuilder.Sql(string.Format(RenameColumn, "imagens_produto", "UrlImagem", "ImageUrl"));
            migrationBuilder.Sql(string.Format(RenameColumn, "imagens_produto", "ProdutoId", "ProductId"));

            // Colunas — produtos
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "CriadoEm", "CreatedAt"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "Ativo", "IsActive"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "QuantidadeEstoque", "StockQuantity"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "PrecoVenda", "SalePrice"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "PrecoCusto", "CostPrice"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "Descricao", "Description"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "Nome", "Name"));
            migrationBuilder.Sql(string.Format(RenameColumn, "produtos", "CategoriaId", "CategoryId"));

            // Colunas — categorias
            migrationBuilder.Sql(string.Format(RenameColumn, "categorias", "Ativo", "IsActive"));
            migrationBuilder.Sql(string.Format(RenameColumn, "categorias", "Descricao", "Description"));
            migrationBuilder.Sql(string.Format(RenameColumn, "categorias", "Nome", "Name"));

            // Colunas — enderecos
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Padrao", "IsDefault"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Estado", "State"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Cidade", "City"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Bairro", "Neighborhood"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Complemento", "Complement"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Numero", "Number"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "Logradouro", "Street"));
            migrationBuilder.Sql(string.Format(RenameColumn, "enderecos", "UsuarioId", "UserId"));

            // Colunas — usuarios
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "CriadoEm", "CreatedAt"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "Papel", "Role"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "Telefone", "Phone"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "IdGoogle", "GoogleId"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "HashSenha", "PasswordHash"));
            migrationBuilder.Sql(string.Format(RenameColumn, "usuarios", "Nome", "Name"));

            // Tabelas
            migrationBuilder.Sql(string.Format(RenameTable, "registros_whatsapp", "whatsapp_logs"));
            migrationBuilder.Sql(string.Format(RenameTable, "notas_fiscais", "invoices"));
            migrationBuilder.Sql(string.Format(RenameTable, "despesas", "expenses"));
            migrationBuilder.Sql(string.Format(RenameTable, "itens_pedido", "order_items"));
            migrationBuilder.Sql(string.Format(RenameTable, "pedidos", "orders"));
            migrationBuilder.Sql(string.Format(RenameTable, "configuracao_frete", "shipping_config"));
            migrationBuilder.Sql(string.Format(RenameTable, "imagens_produto", "product_images"));
            migrationBuilder.Sql(string.Format(RenameTable, "produtos", "products"));
            migrationBuilder.Sql(string.Format(RenameTable, "categorias", "categories"));
            migrationBuilder.Sql(string.Format(RenameTable, "enderecos", "addresses"));
            migrationBuilder.Sql(string.Format(RenameTable, "usuarios", "users"));
        }
    }
}