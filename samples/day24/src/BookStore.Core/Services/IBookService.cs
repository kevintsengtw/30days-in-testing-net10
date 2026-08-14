using System.Threading;

namespace BookStore.Core.Services;

/// <summary>
/// 書籍業務邏輯介面
/// </summary>
public interface IBookService
{
    /// <summary>
    /// 建立新書籍
    /// </summary>
    Task<Book> CreateBookAsync(
        string title,
        string author,
        decimal price,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據 ID 取得書籍
    /// </summary>
    Task<Book?> GetBookAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得所有書籍
    /// </summary>
    Task<IEnumerable<Book>> GetAllBooksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新書籍價格
    /// </summary>
    Task<Book> UpdateBookPriceAsync(
        int id,
        decimal newPrice,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除書籍
    /// </summary>
    Task DeleteBookAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 搜尋作者的書籍
    /// </summary>
    Task<IEnumerable<Book>> SearchBooksByAuthorAsync(
        string? author,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得推薦書籍（價格在合理範圍內）
    /// </summary>
    Task<IEnumerable<Book>> GetRecommendedBooksAsync(CancellationToken cancellationToken = default);
}
