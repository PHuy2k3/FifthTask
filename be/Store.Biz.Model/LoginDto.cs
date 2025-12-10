using System.ComponentModel.DataAnnotations;

namespace Store.Biz.Model;
public class LoginDto
{
    [Required]
    public string Username { get; set; } = null!;
    [Required]
    public string Password { get; set; } = null!;
    public ReplaceCartItemDto[]? CartItems { get; set; }
}
