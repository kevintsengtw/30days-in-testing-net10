using System.Threading;

namespace BookStore.Core.Repositories;

/// <summary>
/// 書籍資料存取介面
/// </summary>
public interface IBookRepository
{
    /// <summary>
    /// 取得所有書籍
    /// </summary>
    Task<IEnumerable<Book>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據 ID 取得書籍
    /// </summary>
    Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增書籍
    /// </summary>
    Task<Book> AddAsync(Book book, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新書籍
    /// </summary>
    Task UpdateAsync(Book book, CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除書籍
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據作者取得書籍
    /// </summary>
    Task<IEnumerable<Book>> GetBooksByAuthorAsync(
        string? author,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得高價書籍
    /// </summary>
    Task<IEnumerable<Book>> GetExpensiveBooksAsync(
        decimal minPrice,
        CancellationToken cancellationToken = default);
}
