using Day07.Refactored;
using Day07.Refactored.Implementations;
using Microsoft.Extensions.Logging;

namespace Day07.Tests;

/// <summary>
/// 這個測試類別展示如何用真實的實作把 <see cref="FileBackupService"/> 組裝起來。
/// 這裡只驗證「以真實相依性可以成功建構服務」，屬於 composition smoke test，
/// 而非真正執行備份行為的整合測試——後者需要真實檔案系統與資料庫，通常會放在獨立的
/// Integration Tests 專案，並以真實資源或 Testcontainers 驗證輸出與副作用。
/// </summary>
public class FileBackupServiceCompositionTests
{
    [Fact]
    public void FileBackupService_以真實相依性組裝_應能成功建立實例()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<FileBackupService>();

        var fileSystem = new FileSystemWrapper();
        var dateTimeProvider = new DateTimeProvider();
        var backupRepository = new SqlBackupRepository("dummy connection string");

        // Act - 以真實相依性組裝服務
        var service = new FileBackupService(fileSystem, dateTimeProvider, backupRepository, logger);

        // Assert - 這是 composition smoke test，只確認服務能被組裝出來。
        // 真正的備份行為（BackupFileAsync）需要實際檔案與資料庫，應在整合測試專案中驗證。
        service.Should().NotBeNull();
    }
}