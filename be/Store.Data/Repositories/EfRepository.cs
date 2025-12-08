using Microsoft.EntityFrameworkCore;
using Store.Data.Interfaces;

namespace Store.Data.Repositories;
public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly StoreDbContext _db;
    private readonly DbSet<T> _set;
    public EfRepository(StoreDbContext db) { _db = db; _set = db.Set<T>(); }

    public async Task AddAsync(T entity) => await _set.AddAsync(entity);
    public IQueryable<T> Query() => _set.AsQueryable();
    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);
    public async Task<List<T>> ListAsync() => await _set.ToListAsync();
    public void Remove(T entity) => _set.Remove(entity);
    public void Update(T entity) => _set.Update(entity);
    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
