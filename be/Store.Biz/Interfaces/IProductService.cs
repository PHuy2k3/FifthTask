using Store.Biz.Model;
namespace Store.Biz.Interfaces;
public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<bool> UpdateAsync(int id, CreateProductDto dto);
    Task<bool> DeleteAsync(int id);
}
