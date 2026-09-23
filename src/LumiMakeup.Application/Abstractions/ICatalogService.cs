using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Application.Abstractions;

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryDto>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> GetActiveProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default);
}