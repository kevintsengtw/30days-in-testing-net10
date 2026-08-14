using Day23.Application.Abstractions;

namespace Day23.Tests.Integration.Infrastructure;

/// <summary>
/// 測試用：模擬基礎設施（資料庫）失敗的 <see cref="IProductRepository"/>。
/// 每個操作都擲出未預期的例外，用來驗證 GlobalExceptionHandler 的 fallback 500 路徑。
/// </summary>
public class ThrowingProductRepository : IProductRepository
{
    private static Exception Fault() => new("模擬資料庫連線失敗");

    public Task<Product> CreateAsync(Product product, CancellationToken cancellationToken = default)
        => throw Fault();

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw Fault();

    public Task<IReadOnlyList<Product>> QueryAsync(
        string? keyword = null,
        int page = 1,
        int pageSize = 20,
        string sort = "created_at",
        string direction = "desc",
        CancellationToken cancellationToken = default)
        => throw Fault();

    public Task<int> CountAsync(string? keyword = null, CancellationToken cancellationToken = default)
        => throw Fault();

    public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        => throw Fault();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw Fault();

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => throw Fault();
}
