using System.Threading;

namespace BookStore.Core.Repositories;

/// <summary>
/// Entity Framework Core 書籍資料存取實作
/// </summary>
public class EfCoreBookRepository : IBookRepository
{
    private readonly BookStoreDbContext _context;

    public EfCoreBookRepository(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Book>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Books
            .OrderBy(b => b.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<Book?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Books
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Book> AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        if (book == null)
            throw new ArgumentNullException(nameof(book));

        if (string.IsNullOrWhiteSpace(book.Title))
            throw new ArgumentException("書籍標題不可為空", nameof(book));

        if (string.IsNullOrWhiteSpace(book.Author))
            throw new ArgumentException("作者不可為空", nameof(book));

        if (book.Price < 0)
            throw new ArgumentException("價格不可為負數", nameof(book));

        // 設定建立時間
        book.CreatedDate = DateTime.UtcNow;

        _context.Books.Add(book);
        await _context.SaveChangesAsync(cancellationToken);

        return book;
    }

    public async Task UpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        if (book == null)
            throw new ArgumentNullException(nameof(book));

        var existingBook = await GetByIdAsync(book.Id, cancellationToken);
        if (existingBook == null)
            throw new InvalidOperationException($"找不到 ID 為 {book.Id} 的書籍");

        // 設定更新時間
        book.UpdatedDate = DateTime.UtcNow;

        _context.Entry(existingBook).CurrentValues.SetValues(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var book = await GetByIdAsync(id, cancellationToken);
        if (book == null)
            throw new InvalidOperationException($"找不到 ID 為 {id} 的書籍");

        _context.Books.Remove(book);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetBooksByAuthorAsync(
        string? author,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(author))
            return Enumerable.Empty<Book>();

        return await _context.Books
            .Where(b => b.Author.Contains(author))
            .OrderBy(b => b.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Book>> GetExpensiveBooksAsync(
        decimal minPrice,
        CancellationToken cancellationToken = default)
    {
        return await _context.Books
            .Where(b => b.Price >= minPrice)
            .OrderByDescending(b => b.Price)
            .ToListAsync(cancellationToken);
    }
}
