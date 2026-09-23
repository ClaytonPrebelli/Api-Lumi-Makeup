using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Application.DTOs;

public sealed record CategoryDto(long Id, string Name, string Slug, string? Description, bool IsActive);

public sealed record ProductImageDto(long Id, string ImageUrl, int SortOrder);

public sealed record ProductDto(
    long Id,
    string Name,
    string Slug,
    string Description,
    decimal SalePrice,
    int StockQuantity,
    bool IsActive,
    long CategoryId,
    string CategoryName,
    IReadOnlyList<ProductImageDto> Images);

public sealed record OrderDto(
    long Id,
    OrderStatus Status,
    DeliveryStatus DeliveryStatus,
    PaymentMethod? PaymentMethod,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    long Id,
    long ProductId,
    string ProductName,
    decimal UnitSalePrice,
    int Quantity,
    decimal Subtotal);