using Day23.Application.Dtos;
using Day23.Application.Services;

namespace Day23.Api.Controllers;

/// <summary>
/// 產品管理控制器
/// </summary>
[ApiController]
[Route("[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// 建立產品
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] ProductCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// 根據 ID 取得產品
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        // 找不到產品時，service 會擲出 KeyNotFoundException，
        // 由 GlobalExceptionHandler 統一對應為 404 ProblemDetails。
        var result = await _productService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 查詢產品列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductResponse>>> Query(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sort = "createdAt",
        [FromQuery] string direction = "desc",
        CancellationToken cancellationToken = default)
    {
        // 排序參數非法時，service 會擲出 ArgumentException，
        // 由 GlobalExceptionHandler 統一對應為 400 ProblemDetails。
        var result = await _productService.QueryAsync(keyword, page, pageSize, sort, direction, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 更新產品
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] ProductUpdateRequest request,
        CancellationToken cancellationToken)
    {
        // 驗證失敗 → ValidationException；找不到產品 → KeyNotFoundException，
        // 兩者都不在 controller 攔截，交由對應的 IExceptionHandler 處理。
        await _productService.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// 刪除產品
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        // 找不到產品時，service 會擲出 KeyNotFoundException，
        // 由 GlobalExceptionHandler 統一對應為 404 ProblemDetails。
        await _productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}