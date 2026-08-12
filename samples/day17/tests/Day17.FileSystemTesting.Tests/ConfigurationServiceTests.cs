using Day17.FileSystemTesting.Core;

namespace Day17.FileSystemTesting.Tests;

/// <summary>
/// ConfigurationService 的單元測試
/// </summary>
public class ConfigurationServiceTests
{
    [Fact]
    public async Task LoadConfigurationAsync_當檔案存在_應回傳檔案內容()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\app.config");
        var expectedContent = "database_connection=server123";

        mockFileSystem.AddFile(filePath, new MockFileData(expectedContent));

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadConfigurationAsync(filePath);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task LoadConfigurationAsync_當檔案不存在_應回傳預設值()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\nonexistent.config");
        var defaultValue = "default_config";

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadConfigurationAsync(filePath, defaultValue);

        // Assert
        result.Should().Be(defaultValue);
    }

    [Fact]
    public async Task LoadConfigurationAsync_當檔案讀取發生例外_應回傳預設值()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IFileSystem>();
        var mockFile = Substitute.For<IFile>();
        mockFileSystem.File.Returns(mockFile);

        var filePath = TestPaths.FromWindowsPath(@"C:\config\app.config");
        var defaultValue = "fallback_config";

        mockFile.Exists(filePath).Returns(true);
        mockFile
            .ReadAllTextAsync(filePath, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new UnauthorizedAccessException("存取被拒")));

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadConfigurationAsync(filePath, defaultValue);

        // Assert
        result.Should().Be(defaultValue);
    }

    [Fact]
    public async Task SaveConfigurationAsync_當目標目錄不存在_應建立目錄並儲存檔案()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\subfolder\app.config");
        var content = "new_configuration";

        var service = new ConfigurationService(mockFileSystem);

        // Act
        await service.SaveConfigurationAsync(filePath, content);

        // Assert
        mockFileSystem.Directory.Exists(TestPaths.FromWindowsPath(@"C:\config\subfolder")).Should().BeTrue();
        mockFileSystem.File.Exists(filePath).Should().BeTrue();
        var savedContent = mockFileSystem.File.ReadAllText(filePath);
        savedContent.Should().Be(content);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SaveConfigurationAsync_當目錄為空或null_應正常儲存到根目錄(string? directory)
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = string.IsNullOrWhiteSpace(directory) ? "app.config" : Path.Combine(directory, "app.config");
        var content = "test_configuration";

        var service = new ConfigurationService(mockFileSystem);

        // Act
        await service.SaveConfigurationAsync(filePath, content);

        // Assert
        mockFileSystem.File.Exists(filePath).Should().BeTrue();
        var savedContent = mockFileSystem.File.ReadAllText(filePath);
        savedContent.Should().Be(content);
    }

    [Fact]
    public async Task LoadJsonConfigurationAsync_當檔案存在且格式正確_應回傳物件()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\settings.json");
        var testConfig = new TestConfiguration
        {
            Name = "Test App",
            Version = "1.0.0",
            IsEnabled = true
        };
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(testConfig);

        mockFileSystem.AddFile(filePath, new MockFileData(jsonContent));

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadJsonConfigurationAsync<TestConfiguration>(filePath);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test App");
        result.Version.Should().Be("1.0.0");
        result.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task LoadJsonConfigurationAsync_當檔案不存在_應回傳null()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\nonexistent.json");

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadJsonConfigurationAsync<TestConfiguration>(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadJsonConfigurationAsync_當JSON格式錯誤_應回傳null()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\invalid.json");
        var invalidJson = "{ invalid json content }";

        mockFileSystem.AddFile(filePath, new MockFileData(invalidJson));

        var service = new ConfigurationService(mockFileSystem);

        // Act
        var result = await service.LoadJsonConfigurationAsync<TestConfiguration>(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveJsonConfigurationAsync_應正確序列化並儲存物件()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\settings.json");
        var testConfig = new TestConfiguration
        {
            Name = "Test App",
            Version = "2.0.0",
            IsEnabled = false
        };

        var service = new ConfigurationService(mockFileSystem);

        // Act
        await service.SaveJsonConfigurationAsync(filePath, testConfig);

        // Assert
        mockFileSystem.File.Exists(filePath).Should().BeTrue();

        var savedContent = mockFileSystem.File.ReadAllText(filePath);
        var deserializedConfig = System.Text.Json.JsonSerializer.Deserialize<TestConfiguration>(savedContent);

        deserializedConfig.Should().NotBeNull();
        deserializedConfig!.Name.Should().Be("Test App");
        deserializedConfig.Version.Should().Be("2.0.0");
        deserializedConfig.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SaveJsonConfigurationAsync_當目標目錄不存在_應建立目錄()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        var filePath = TestPaths.FromWindowsPath(@"C:\config\deep\nested\settings.json");
        var testConfig = new TestConfiguration { Name = "Test" };

        var service = new ConfigurationService(mockFileSystem);

        // Act
        await service.SaveJsonConfigurationAsync(filePath, testConfig);

        // Assert
        mockFileSystem.Directory.Exists(TestPaths.FromWindowsPath(@"C:\config\deep\nested")).Should().BeTrue();
        mockFileSystem.File.Exists(filePath).Should().BeTrue();
    }

    /// <summary>
    /// 測試用的設定類別
    /// </summary>
    public class TestConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}
