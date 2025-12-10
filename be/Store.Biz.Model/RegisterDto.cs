using System.ComponentModel.DataAnnotations;

namespace Store.Biz.Model;

public class RegisterDto
{
    [Required]
    public string Username { get; set; } = null!;

    [Required, EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}