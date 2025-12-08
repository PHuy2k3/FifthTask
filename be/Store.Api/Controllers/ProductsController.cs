using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Store.Biz.Interfaces;
using Store.Biz.Model;

namespace Store.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _svc;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService svc, ILogger<ProductsController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var list = await _svc.GetAllAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAll products failed");
            return Problem(detail: "Internal server error", statusCode: 500);
        }
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(int id)
    {
        try
        {
            var p = await _svc.GetByIdAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get product {Id} failed", id);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _svc.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create product failed");
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateProductDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var ok = await _svc.UpdateAsync(id, dto);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update product {Id} failed", id);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var ok = await _svc.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete product {Id} failed", id);
            return Problem(detail: ex.Message, statusCode: 500);
        }
    }
}
