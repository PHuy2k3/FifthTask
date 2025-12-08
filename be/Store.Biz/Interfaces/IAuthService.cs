using Store.Biz.Model;
namespace Store.Biz.Interfaces;
public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterDto dto);
    Task<AuthResultDto?> LoginAsync(LoginDto dto);
    Task<List<UserDto>> GetAllUsersAsync();
    Task<bool> PromoteToAdminAsync(int userId);
    Task<bool> DeleteUserAsync(int userId);

}
