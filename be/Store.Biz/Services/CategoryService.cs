using Microsoft.EntityFrameworkCore;
using Store.Biz.Interfaces;
using Store.Biz.Model;
using Store.Data;
using Store.Data.Interfaces;
using Store.Data.Model;

namespace Store.Biz.Services;

public class CategoryService : ICategoryService
{
    private readonly IRepository<Category> _repo;
    private readonly StoreDbContext _db;

    public CategoryService(IRepository<Category> repo, StoreDbContext db)
    {
        _repo = repo;
        _db = db;
    }


    public async Task<List<CategoryDto>> GetAllAsync()
    {
        return await _repo.Query()
            .AsNoTracking()
            .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
            .ToListAsync();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var c = await _repo.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return null;
        return new CategoryDto { Id = c.Id, Name = c.Name };
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        var c = new Category { Name = dto.Name };
        await _repo.AddAsync(c);
        await _repo.SaveChangesAsync();
        return new CategoryDto { Id = c.Id, Name = c.Name };
    }

    public async Task<bool> UpdateAsync(int id, CreateCategoryDto dto)
    {
        var c = await _repo.GetByIdAsync(id);
        if (c == null) return false;
        c.Name = dto.Name;
        _repo.Update(c);
        await _repo.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var c = await _repo.GetByIdAsync(id);
        if (c == null) return false;

        // Check nếu category đang được dùng
        var hasProducts = await _db.Products.AnyAsync(p => p.CategoryId == id);
        if (hasProducts)
            throw new InvalidOperationException("Category has products. Remove or reassign products first.");

        _repo.Remove(c);
        await _repo.SaveChangesAsync();
        return true;
    }

}
