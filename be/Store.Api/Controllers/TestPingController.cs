using Microsoft.AspNetCore.Mvc;

namespace Store.Api.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestPingController : ControllerBase
    {
        [HttpGet("ping")]
        public IActionResult Ping() => Ok(new { ok = true, now = System.DateTime.UtcNow });
    }
}
