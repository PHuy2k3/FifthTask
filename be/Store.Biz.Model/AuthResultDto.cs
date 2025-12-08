namespace Store.Biz.Model;
public class AuthResultDto
{
    public string Token { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Role { get; set; } = null!;
}
