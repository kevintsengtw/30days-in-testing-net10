using BookStore.Tests.Infrastructure;
using BookStore.Tests.Helpers;

namespace BookStore.Tests.Integration;

/// <summary>
/// 交易處理與並發測試
/// </summary>
[Collection("AspireApp")]
public class TransactionTests : IntegrationTestBase
{
    private readonly AspireAppFixture _fixture;

    public TransactionTests(AspireAppFixture fixture) : base(fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateBooks_使用交易_失敗時應完整回滾()
    {
        // Arrange
        using var dbContext = await _fixture.GetDbContextWithoutRetryAsync();
        using var transaction = await dbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var validBook = new Book { Title = "有效書籍", Author = "作者", Price = 100m };
        var invalidBook = new Book { Title = null!, Author = "作者", Price = 100m };

        // Act
        dbContext.Books.Add(validBook);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        dbContext.Books.Add(invalidBook);
        var action = async () => await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<DbUpdateException>();
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        using var verifyContext = _fixture.GetDbContext();
        var bookCount = await verifyContext.Books.CountAsync(
            b => b.Title == "有效書籍",
            TestContext.Current.CancellationToken);
        bookCount.Should().Be(0, "交易回滾後，所有資料都應該被撤銷");
    }

    [Fact]
    public async Task ConcurrentBookCreation_多執行緒存取_應保持資料一致性()
    {
        // Arrange
        var tasks = new List<Task<int>>();
        var bookTitles = new List<string>();

        // Act - 模擬 10 個並發的書籍建立操作
        for (int i = 0; i < 10; i++)
        {
            var title = $"並發測試書籍 {i:D2}";
            bookTitles.Add(title);

            tasks.Add(Task.Run(async () =>
            {
                using var dbContext = _fixture.GetDbContext();
                var book = new Book
                {
                    Title = title,
                    Author = "並發作者",
                    Price = 99.99m
                };

                dbContext.Books.Add(book);
                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                return book.Id;
            }));
        }

        var bookIds = await Task.WhenAll(tasks);

        // Assert
        bookIds.Should().HaveCount(10, "所有並發操作都應該成功");
        bookIds.Should().OnlyHaveUniqueItems("每本書都應該有唯一的 ID");

        // 驗證資料庫中的資料完整性
        using var verifyContext = _fixture.GetDbContext();
        var savedBooks = await verifyContext.Books
            .Where(b => b.Author == "並發作者")
            .ToListAsync(TestContext.Current.CancellationToken);

        savedBooks.Should().HaveCount(10, "所有書籍都應該被正確儲存");

        foreach (var expectedTitle in bookTitles)
        {
            savedBooks.Should().Contain(b => b.Title == expectedTitle,
                $"應該包含書籍: {expectedTitle}");
        }
    }
}
