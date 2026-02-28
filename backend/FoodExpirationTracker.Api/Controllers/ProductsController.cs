using FoodExpirationTracker.Api.Extensions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodExpirationTracker.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetInventory(
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = HttpContext.GetRequiredUserId();
        var items = await _productService.GetUserInventoryAsync(userId, category, page, pageSize, cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> AddProduct([FromBody] AddProductRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        var result = await _productService.AddProductBatchAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] ConsumeBatchRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _productService.ConsumeBatchAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("batch/{batchId:guid}")]
    public async Task<IActionResult> DeleteBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _productService.DeleteBatchAsync(userId, batchId, cancellationToken);
        return NoContent();
    }
}
