using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Biz.Interfaces;

namespace Store.Api.Controllers;
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAuthService _auth;
    public AdminController(IAuthService auth) => _auth = auth;

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() => Ok(await _auth.GetAllUsersAsync());

    [HttpPost("promote/{id:int}")]
    public async Task<IActionResult> Promote(int id)
    {
        var ok = await _auth.PromoteToAdminAsync(id);
        return ok ? NoContent() : NotFound();
    }
}
