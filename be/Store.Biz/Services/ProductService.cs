using Microsoft.EntityFrameworkCore;
using Store.Biz.Interfaces;
using Store.Biz.Model;
using Store.Data;
using Store.Data.Model;

namespace Store.Biz.Services;

public class ProductService : IProductService
{
    private readonly StoreDbContext _db;

    public ProductService(StoreDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductDto>> GetAllAsync()
    {
        var q = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Name);

        var list = await q.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            CategoryId = p.CategoryId,
            CategoryName = p.Category != null ? p.Category.Name : null
        }).ToListAsync();

        return list;
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        // Use Include so EF can translate to SQL and bring category
        var p = await _db.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return null;

        return new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name
        };
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var p = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Stock = dto.Stock,
            CategoryId = dto.CategoryId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Products.Add(p);
        await _db.SaveChangesAsync();

        // Load category name if any
        string? catName = null;
        if (p.CategoryId.HasValue)
        {
            var cat = await _db.Categories.FindAsync(p.CategoryId.Value);
            catName = cat?.Name;
        }

        return new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            CategoryId = p.CategoryId,
            CategoryName = catName
        };
    }

    public async Task<bool> UpdateAsync(int id, CreateProductDto dto)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return false;

        p.Name = dto.Name;
        p.Description = dto.Description;
        p.Price = dto.Price;
        p.Stock = dto.Stock;
        p.CategoryId = dto.CategoryId;
        p.IsActive = true;

        _db.Products.Update(p);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return false;

        _db.Products.Remove(p);
        await _db.SaveChangesAsync();
        return true;
    }
}
