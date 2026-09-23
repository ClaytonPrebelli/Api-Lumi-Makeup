using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class CatalogService : ICatalogService
{
    private readonly LumiDbContext _dbContext;

    public CatalogService(LumiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductDto>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Description,
                p.SalePrice,
                p.StockQuantity,
                p.IsActive,
                p.CategoryId,
                p.Category.Name,
                p.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.SortOrder))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.IsActive)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Description,
                p.SalePrice,
                p.StockQuantity,
                p.IsActive,
                p.CategoryId,
                p.Category.Name,
                p.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.SortOrder))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}