---
day: 3
title: "Day 03 - xUnit 進階功能與測試資料管理"
sample: samples/day03
target_framework: net10.0
packages:
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
  - Microsoft.Testing.Extensions.TrxReport
  - Microsoft.EntityFrameworkCore
  - Microsoft.EntityFrameworkCore.InMemory
  - Microsoft.EntityFrameworkCore.SqlServer
  - Microsoft.Data.SqlClient
---

# Day 03 - xUnit 進階功能與測試資料管理

<!-- toc -->

- [前言：從基礎到進階的躍進](#前言從基礎到進階的躍進)
- [今日目標](#今日目標)
- [Theory 進階資料提供機制](#theory-進階資料提供機制)
- [測試資料重複使用策略](#測試資料重複使用策略)
- [xUnit 進階資源管理：Fixture 深度應用](#xunit-進階資源管理fixture-深度應用)
- [xUnit 並行執行基礎](#xunit-並行執行基礎)
- [測試資料與資源管理策略選擇](#測試資料與資源管理策略選擇)
- [今日重點回顧](#今日重點回顧)
- [明日預告](#明日預告)
- [參考資源](#參考資源)
- [老派工程師的心得](#老派工程師的心得)

<!-- /toc -->

## 前言：從基礎到進階的躍進

Day 02 完成了框架選擇、基本語法與第一個測試專案。這一篇接著處理 xUnit 的進階功能：複雜測試資料、執行效能，以及可維護的測試結構。

**為什麼需要進階測試資料管理？**

隨著專案規模成長，你會發現：

- 測試資料變得複雜且難以維護
- 相同的測試資料在多個測試中重複
- 測試執行時間越來越長
- 測試間的資源衝突開始出現

---

## 今日目標

- 掌握 Theory 的進階資料提供機制：MemberData、ClassData、PropertyData
- 學習測試資料的組織與重用策略
- 深度應用 xUnit 的資源共享：IClassFixture、ICollectionFixture
- 認識 xUnit 並行執行原理與效能最佳化實務
- 建立可維護的測試資料管理實踐

---

## Theory 進階資料提供機制

### 回顧：InlineData 的限制

在 Day 02 我們學習了 InlineData 的基本用法：

```csharp
[Theory]
[InlineData(1, 2, 3)]
[InlineData(-1, 1, 0)]
[InlineData(0, 0, 0)]
public void Add_基本測試_應回傳正確結果(int a, int b, int expected)
{
    // 測試邏輯
}
```

**InlineData 的限制**：

- 只能使用編譯時常數
- 不支援複雜物件
- 無法動態產生測試資料
- 資料重複使用困難

### MemberData：靜態成員提供測試資料

#### 基本 MemberData 用法

```csharp
public class CalculatorAdvancedTests
{
    private readonly Calculator _calculator;
    
    public CalculatorAdvancedTests()
    {
        _calculator = new Calculator();
    }
    
    // 使用靜態屬性提供測試資料
    public static IEnumerable<object[]> AddTestData =>
        new List<object[]>
        {
            new object[] { 1, 2, 3 },
            new object[] { -1, 1, 0 },
            new object[] { 0, 0, 0 },
            new object[] { 100, 200, 300 },
            new object[] { int.MaxValue, 1, (long)int.MaxValue + 1 } // 溢位測試
        };
    
    [Theory]
    [MemberData(nameof(AddTestData))]
    public void Add_使用MemberData_應回傳正確結果(int a, int b, long expected)
    {
        // Act
        var result = _calculator.Add(a, b);
        
        // Assert
        Assert.Equal(expected, result);
    }
}
```

#### 使用靜態方法提供動態資料

```csharp
public class StringValidationTests
{
    // 使用靜態方法動態產生測試資料
    public static IEnumerable<object[]> GetEmailTestData()
    {
        // 有效的 Email 格式
        yield return new object[] { "test@example.com", true };
        yield return new object[] { "user.name@company.co.uk", true };
        yield return new object[] { "admin+tag@service.org", true };
        
        // 無效的 Email 格式
        yield return new object[] { "invalid-email", false };
        yield return new object[] { "@example.com", false };
        yield return new object[] { "test@", false };
        yield return new object[] { "", false };
        yield return new object[] { null, false };
        
        // 邊界值測試
        yield return new object[] { new string('a', 64) + "@example.com", true }; // 最大長度
        yield return new object[] { new string('a', 65) + "@example.com", false }; // 超過最大長度
    }
    
    [Theory]
    [MemberData(nameof(GetEmailTestData))]
    public void IsValidEmail_各種格式_應回傳正確驗證結果(string email, bool expected)
    {
        // Arrange
        var validator = new EmailValidator();
        
        // Act
        var result = validator.IsValidEmail(email);
        
        // Assert
        Assert.Equal(expected, result);
    }
}
```

#### 跨類別共享 MemberData

```csharp
public static class CommonTestData
{
    public static IEnumerable<object[]> GetValidUserData()
    {
        yield return new object[] 
        { 
            new User { Name = "John Doe", Email = "john@example.com", Age = 30 } 
        };
        yield return new object[] 
        { 
            new User { Name = "Jane Smith", Email = "jane@company.com", Age = 25 } 
        };
    }
    
    public static IEnumerable<object[]> GetInvalidUserData()
    {
        yield return new object[] 
        { 
            new User { Name = "", Email = "john@example.com", Age = 30 } // 空名稱
        };
        yield return new object[] 
        { 
            new User { Name = "John", Email = "invalid-email", Age = 30 } // 無效 Email
        };
        yield return new object[] 
        { 
            new User { Name = "John", Email = "john@example.com", Age = -1 } // 無效年齡
        };
    }
}

public class UserServiceTests
{
    [Theory]
    [MemberData(nameof(CommonTestData.GetValidUserData), MemberType = typeof(CommonTestData))]
    public void CreateUser_有效使用者資料_應成功建立(User user)
    {
        // 測試邏輯
    }
    
    [Theory]
    [MemberData(nameof(CommonTestData.GetInvalidUserData), MemberType = typeof(CommonTestData))]
    public void CreateUser_無效使用者資料_應拋出例外(User user)
    {
        // 測試邏輯
    }
}
```

### ClassData：專用資料類別

#### 基本 ClassData 實作

```csharp
public class CalculationTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        // 基本運算測試
        yield return new object[] { 10, 2, 5.0, "divide" };
        yield return new object[] { 7, 2, 3.5, "divide" };
        yield return new object[] { -10, 2, -5.0, "divide" };
        
        // 乘法測試
        yield return new object[] { 5, 3, 15.0, "multiply" };
        yield return new object[] { -2, 4, -8.0, "multiply" };
        yield return new object[] { 0, 100, 0.0, "multiply" };
        
        // 邊界值測試
        yield return new object[] { double.MaxValue, 2, double.MaxValue / 2, "divide" };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[Theory]
[ClassData(typeof(CalculationTestData))]
public void Calculate_使用ClassData_應回傳正確結果(double a, double b, double expected, string operation)
{
    // Arrange
    var calculator = new Calculator();
    
    // Act
    var result = operation switch
    {
        "divide" => calculator.Divide(a, b),
        "multiply" => calculator.Multiply(a, b),
        _ => throw new ArgumentException("Unknown operation")
    };
    
    // Assert
    Assert.Equal(expected, result, precision: 2);
}
```

#### 參數化 ClassData

```csharp
public class DatabaseConnectionTestData : IEnumerable<object[]>
{
    private readonly string _connectionString;
    
    public DatabaseConnectionTestData(string connectionString = "DefaultConnection")
    {
        _connectionString = connectionString;
    }
    
    public IEnumerator<object[]> GetEnumerator()
    {
        // 根據不同的連線字串產生不同的測試資料
        if (_connectionString == "DefaultConnection")
        {
            yield return new object[] { "SELECT * FROM Users", 10 };
            yield return new object[] { "SELECT COUNT(*) FROM Products", 1 };
        }
        else
        {
            yield return new object[] { "SELECT * FROM TestUsers", 5 };
            yield return new object[] { "SELECT COUNT(*) FROM TestProducts", 1 };
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

#### 進階 ClassData：整合外部資料源

```csharp
public class CsvTestData : IEnumerable<object[]>
{
    private readonly string _csvFilePath;
    
    public CsvTestData(string csvFilePath = "TestData/calculations.csv")
    {
        _csvFilePath = csvFilePath;
    }
    
    public IEnumerator<object[]> GetEnumerator()
    {
        if (!File.Exists(_csvFilePath))
        {
            // 如果檔案不存在，提供預設測試資料
            yield return new object[] { 1, 2, 3 };
            yield return new object[] { 5, 5, 10 };
            yield break;
        }
        
        var lines = File.ReadAllLines(_csvFilePath);
        foreach (var line in lines.Skip(1)) // 跳過標題行
        {
            var values = line.Split(',');
            if (values.Length >= 3 && 
                int.TryParse(values[0], out var a) &&
                int.TryParse(values[1], out var b) &&
                int.TryParse(values[2], out var expected))
            {
                yield return new object[] { a, b, expected };
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[Theory]
[ClassData(typeof(CsvTestData))]
public void Add_使用CSV資料_應回傳正確結果(int a, int b, int expected)
{
    // 測試邏輯
}
```

### PropertyData：屬性驅動的測試資料

```csharp
public class ComplexObjectTests
{
    // 複雜物件的測試資料
    public static IEnumerable<object[]> UserScenarios
    {
        get
        {
            yield return new object[]
            {
                new User 
                { 
                    Name = "Admin User", 
                    Email = "admin@company.com", 
                    Roles = new[] { "Admin", "User" },
                    Settings = new UserSettings { Theme = "Dark", Language = "zh-TW" }
                },
                true // 預期結果：可以存取管理功能
            };
            
            yield return new object[]
            {
                new User 
                { 
                    Name = "Regular User", 
                    Email = "user@company.com", 
                    Roles = new[] { "User" },
                    Settings = new UserSettings { Theme = "Light", Language = "en-US" }
                },
                false // 預期結果：無法存取管理功能
            };
        }
    }
    
    [Theory]
    [MemberData(nameof(UserScenarios))]
    public void CanAccessAdminFeatures_不同使用者角色_應回傳正確權限(User user, bool expected)
    {
        // Arrange
        var authService = new AuthorizationService();
        
        // Act
        var result = authService.CanAccessAdminFeatures(user);
        
        // Assert
        Assert.Equal(expected, result);
    }
}
```

---

## 測試資料重複使用策略

### Test Data Builder 模式

#### 基本 Builder 實作

```csharp
public class UserBuilder
{
    private string _name = "Default User";
    private string _email = "default@example.com";
    private int _age = 25;
    private List<string> _roles = new();
    private UserSettings _settings = new();
    
    public UserBuilder WithName(string name)
    {
        _name = name;
        return this;
    }
    
    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }
    
    public UserBuilder WithAge(int age)
    {
        _age = age;
        return this;
    }
    
    public UserBuilder WithRole(string role)
    {
        _roles.Add(role);
        return this;
    }
    
    public UserBuilder WithRoles(params string[] roles)
    {
        _roles.AddRange(roles);
        return this;
    }
    
    public UserBuilder WithAdminRights()
    {
        return WithRoles("Admin", "User");
    }
    
    public UserBuilder WithSettings(UserSettings settings)
    {
        _settings = settings;
        return this;
    }
    
    public User Build()
    {
        return new User
        {
            Name = _name,
            Email = _email,
            Age = _age,
            Roles = _roles.ToArray(),
            Settings = _settings
        };
    }
    
    // 預設建立者方法
    public static UserBuilder AUser() => new();
    public static UserBuilder AnAdminUser() => new UserBuilder().WithAdminRights();
    public static UserBuilder ARegularUser() => new UserBuilder().WithRole("User");
}
```

#### Builder 在測試中的應用

```csharp
public class UserServiceTests
{
    [Fact]
    public void CreateUser_有效的管理員使用者_應成功建立()
    {
        // Arrange - 使用 Builder 模式建立測試資料
        var adminUser = UserBuilder
            .AnAdminUser()
            .WithName("John Admin")
            .WithEmail("john.admin@company.com")
            .WithAge(35)
            .Build();
            
        var userService = new UserService();
        
        // Act
        var result = userService.CreateUser(adminUser);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("John Admin", result.Name);
        Assert.Contains("Admin", result.Roles);
    }
    
    [Theory]
    [MemberData(nameof(GetUserScenarios))]
    public void ValidateUser_不同使用者情境_應回傳正確驗證結果(User user, bool expected)
    {
        // Arrange
        var validator = new UserValidator();
        
        // Act
        var result = validator.IsValid(user);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    public static IEnumerable<object[]> GetUserScenarios()
    {
        // 有效使用者情境
        yield return new object[]
        {
            UserBuilder.AUser()
                .WithName("Valid User")
                .WithEmail("valid@example.com")
                .WithAge(25)
                .Build(),
            true
        };
        
        // 無效使用者情境 - 空名稱
        yield return new object[]
        {
            UserBuilder.AUser()
                .WithName("")
                .WithEmail("valid@example.com")
                .WithAge(25)
                .Build(),
            false
        };
        
        // 無效使用者情境 - 年齡過小
        yield return new object[]
        {
            UserBuilder.AUser()
                .WithName("Young User")
                .WithEmail("young@example.com")
                .WithAge(10)
                .Build(),
            false
        };
    }
}
```

其實這個 Test Builder 在已經有寫單元測試好一段時間的開發者來說，應該會想「這個模式不就是用 AutoFixture 的 Builder 方式嗎？」

這邊先介紹 Test Builder Pattern，之後會再介紹 AutoFixture。

Test Data Builder 模式是 Object Mother 模式的改良版，解決了這些問題：

- Object Mother 的測試資料過於固定
- 難以針對特定測試情境調整資料
- 測試資料的意圖不夠明確

相關連結：

- [Test Data Builders: an alternative to the Object Mother pattern | Nat Pryce](http://www.natpryce.com/articles/000714.html)
- [Test Data Builders and Object Mother: another look](https://www.javacodegeeks.com/2014/06/test-data-builders-and-object-mother-another-look.html)
- [测试数据构建器和对象母亲：另一种眼神](https://blog.csdn.net/dnc8371/article/details/106704415)


### 資料提供者模式

#### 通用資料提供者介面

```csharp
public interface ITestDataProvider<T>
{
    IEnumerable<T> GetValidData();
    IEnumerable<T> GetInvalidData();
    IEnumerable<T> GetBoundaryData();
    T GetSampleData();
}

public class UserTestDataProvider : ITestDataProvider<User>
{
    public IEnumerable<User> GetValidData()
    {
        yield return UserBuilder.AUser()
            .WithName("John Doe")
            .WithEmail("john@example.com")
            .WithAge(30)
            .Build();
            
        yield return UserBuilder.AnAdminUser()
            .WithName("Admin User")
            .WithEmail("admin@company.com")
            .WithAge(35)
            .Build();
    }
    
    public IEnumerable<User> GetInvalidData()
    {
        yield return UserBuilder.AUser()
            .WithName("")
            .WithEmail("john@example.com")
            .WithAge(30)
            .Build();
            
        yield return UserBuilder.AUser()
            .WithName("John")
            .WithEmail("invalid-email")
            .WithAge(30)
            .Build();
    }
    
    public IEnumerable<User> GetBoundaryData()
    {
        yield return UserBuilder.AUser()
            .WithAge(18) // 最小年齡
            .Build();
            
        yield return UserBuilder.AUser()
            .WithAge(120) // 最大年齡
            .Build();
    }
    
    public User GetSampleData()
    {
        return UserBuilder.AUser().Build();
    }
}
```

#### 在測試中使用資料提供者

```csharp
public class UserValidationTests
{
    private readonly ITestDataProvider<User> _userDataProvider;
    private readonly UserValidator _validator;
    
    public UserValidationTests()
    {
        _userDataProvider = new UserTestDataProvider();
        _validator = new UserValidator();
    }
    
    [Theory]
    [MemberData(nameof(GetValidUsers))]
    public void ValidateUser_有效使用者_應通過驗證(User user)
    {
        // Act
        var result = _validator.IsValid(user);
        
        // Assert
        Assert.True(result);
    }
    
    [Theory]
    [MemberData(nameof(GetInvalidUsers))]
    public void ValidateUser_無效使用者_應驗證失敗(User user)
    {
        // Act
        var result = _validator.IsValid(user);
        
        // Assert
        Assert.False(result);
    }
    
    public static IEnumerable<object[]> GetValidUsers()
    {
        var provider = new UserTestDataProvider();
        return provider.GetValidData().Select(user => new object[] { user });
    }
    
    public static IEnumerable<object[]> GetInvalidUsers()
    {
        var provider = new UserTestDataProvider();
        return provider.GetInvalidData().Select(user => new object[] { user });
    }
}
```

---

## xUnit 進階資源管理：Fixture 深度應用

### IClassFixture：類別層級的資源共享

#### 基本 IClassFixture 用法

以下這個 DatabaseFixture 是用來建立測試用的 LocalDB

```csharp
// 資料庫連線 Fixture
public class DatabaseFixture : IDisposable
{
    public string ConnectionString { get; }
    public IDbConnection Connection { get; }
    
    public DatabaseFixture()
    {
        // 建立測試資料庫（只執行一次）
        ConnectionString = CreateTestDatabase();
        Connection = new SqlConnection(ConnectionString);
        Connection.Open();
        
        // 初始化測試資料
        SeedTestData();
    }
    
    public void Dispose()
    {
        Connection?.Dispose();
        CleanupTestDatabase();
    }
    
    private string CreateTestDatabase()
    {
        // 建立唯一的測試資料庫
        var dbName = $"TestDb_{Guid.NewGuid():N}";
        var masterConnection = "Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=true";
        
        using var connection = new SqlConnection(masterConnection);
        connection.Open();
        
        var createDbCommand = $"CREATE DATABASE [{dbName}]";
        using var command = new SqlCommand(createDbCommand, connection);
        command.ExecuteNonQuery();
        
        return $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=true";
    }
    
    private void SeedTestData()
    {
        // 建立測試資料表和初始資料
        var createTableSql = @"
            CREATE TABLE Users (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                Email NVARCHAR(255) NOT NULL,
                CreatedAt DATETIME2 DEFAULT GETDATE()
            )";
            
        using var command = new SqlCommand(createTableSql, Connection);
        command.ExecuteNonQuery();
        
        // 插入測試資料
        var insertSql = "INSERT INTO Users (Name, Email) VALUES (@Name, @Email)";
        using var insertCommand = new SqlCommand(insertSql, Connection);
        
        var testUsers = new[]
        {
            ("John Doe", "john@example.com"),
            ("Jane Smith", "jane@example.com"),
            ("Admin User", "admin@company.com")
        };
        
        foreach (var (name, email) in testUsers)
        {
            insertCommand.Parameters.Clear();
            insertCommand.Parameters.AddWithValue("@Name", name);
            insertCommand.Parameters.AddWithValue("@Email", email);
            insertCommand.ExecuteNonQuery();
        }
    }
    
    private void CleanupTestDatabase()
    {
        if (!string.IsNullOrEmpty(ConnectionString))
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString);
            var dbName = builder.InitialCatalog;
            
            var masterConnection = "Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=true";
            using var connection = new SqlConnection(masterConnection);
            connection.Open();
            
            var dropDbCommand = $"DROP DATABASE IF EXISTS [{dbName}]";
            using var command = new SqlCommand(dropDbCommand, connection);
            command.ExecuteNonQuery();
        }
    }
}
```

#### 使用 DatabaseFixture 的測試

```csharp
public class UserRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly UserRepository _repository;
    
    public UserRepositoryTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _repository = new UserRepository(_databaseFixture.ConnectionString);
    }
    
    [Fact]
    public void GetAllUsers_應回傳所有測試使用者()
    {
        // Act
        var users = _repository.GetAllUsers();
        
        // Assert
        Assert.NotEmpty(users);
        Assert.Equal(3, users.Count()); // 我們在 Fixture 中插入了 3 個使用者
        Assert.Contains(users, u => u.Name == "John Doe");
        Assert.Contains(users, u => u.Name == "Jane Smith");
        Assert.Contains(users, u => u.Name == "Admin User");
    }
    
    [Fact]
    public void GetUserByEmail_存在的Email_應回傳對應使用者()
    {
        // Arrange
        var email = "john@example.com";
        
        // Act
        var user = _repository.GetUserByEmail(email);
        
        // Assert
        Assert.NotNull(user);
        Assert.Equal("John Doe", user.Name);
        Assert.Equal(email, user.Email);
    }
    
    [Fact]
    public void CreateUser_新使用者_應成功建立()
    {
        // Arrange
        var newUser = new User 
        { 
            Name = "Test User", 
            Email = "test@example.com" 
        };
        
        // Act
        var createdUser = _repository.CreateUser(newUser);
        
        // Assert
        Assert.NotNull(createdUser);
        Assert.True(createdUser.Id > 0);
        Assert.Equal(newUser.Name, createdUser.Name);
        Assert.Equal(newUser.Email, createdUser.Email);
        
        // 驗證資料確實儲存到資料庫
        var retrievedUser = _repository.GetUserByEmail(newUser.Email);
        Assert.NotNull(retrievedUser);
        Assert.Equal(newUser.Name, retrievedUser.Name);
    }
}
```

有關資料存取層的測試，上面的範例是使用 LocalDB 建立測試用資料庫，但這有個環境限制，因為 LocalDB 只能在 Windows 環境下建立與執行。

所以比較適當的方式應該就是整合 `Testcontainers` 或 `.NET Aspire`，這在後續的篇章裡將會介紹。

也有人會用 SQLite 取代正式資料庫。簡單的 CRUD 測試確實可行，但實務專案的資料處理通常更複雜；只要用到特定資料庫的 SQL 語法或功能，SQLite 就無法重現相同行為。


### ICollectionFixture：集合層級的資源共享

#### 建立 Collection Fixture

```csharp
// 使用 WebApplicationFactory 的整合測試 Fixture
public class WebApiFixture : IDisposable
{
    public HttpClient Client { get; }
    public WebApplicationFactory<Program> Factory { get; }
    
    public WebApiFixture()
    {
        // 使用 WebApplicationFactory 建立測試伺服器
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                
                // 覆寫服務註冊以使用測試專用的實作
                builder.ConfigureServices(services =>
                {
                    // 移除原本的資料庫註冊
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);
                    
                    // 使用 In-Memory 資料庫
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
                    
                    // 替換外部服務為 Mock 實作
                    services.AddScoped<IEmailService, MockEmailService>();
                    services.AddScoped<IPaymentService, MockPaymentService>();
                });
                
                // 設定測試專用的組態
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["ConnectionStrings:DefaultConnection"] = "InMemoryTestDb",
                        ["ApiKeys:ExternalService"] = "test-api-key",
                        ["Features:EnableNewFeature"] = "true"
                    });
                });
            });
        
        // 建立 HTTP 用戶端
        Client = Factory.CreateClient();
        
        // 初始化測試資料
        SeedTestData();
    }
    
    private void SeedTestData()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // 確保資料庫已建立
        dbContext.Database.EnsureCreated();
        
        // 插入測試資料
        var testUsers = new[]
        {
            new User { Name = "API Test User 1", Email = "apitest1@example.com" },
            new User { Name = "API Test User 2", Email = "apitest2@example.com" }
        };
        
        dbContext.Users.AddRange(testUsers);
        dbContext.SaveChanges();
    }
    
    // 取得服務執行個體的輔助方法
    public T GetService<T>() where T : notnull
    {
        return Factory.Services.GetRequiredService<T>();
    }
    
    // 建立有認證的用戶端
    public HttpClient CreateAuthenticatedClient(string userId = "test-user")
    {
        var client = Factory.CreateClient();
        
        // 加入 JWT Token 或其他認證資訊
        var token = GenerateTestJwtToken(userId);
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        return client;
    }
    
    private string GenerateTestJwtToken(string userId)
    {
        // 產生測試用的 JWT Token
        // 實際專案中可以使用 JwtSecurityTokenHandler
        return "test-jwt-token";
    }
    
    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}

// 定義 Collection
[CollectionDefinition("WebApi Collection")]
public class WebApiCollection : ICollectionFixture<WebApiFixture>
{
    // 這個類別不需要實作任何程式碼
    // 它只是用來定義 Collection 和關聯的 Fixture
}
```

#### 使用 Collection Fixture 的測試

```csharp
[Collection("WebApi Collection")]
public class UsersControllerTests
{
    private readonly WebApiFixture _fixture;
    private readonly HttpClient _client;
    
    public UsersControllerTests(WebApiFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.Client;
    }
    
    [Fact]
    public async Task GetUsers_應回傳所有使用者()
    {
        // Act
        var response = await _client.GetAsync("/api/users");
        
        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var users = JsonSerializer.Deserialize<User[]>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(users);
        Assert.Equal(2, users.Length);
    }
    
    [Fact]
    public async Task CreateUser_有效使用者資料_應成功建立()
    {
        // Arrange
        var newUser = new { Name = "New API User", Email = "newapi@example.com" };
        var json = JsonSerializer.Serialize(newUser);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        // Act
        var response = await _client.PostAsync("/api/users", content);
        
        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        // 驗證回應內容
        var responseContent = await response.Content.ReadAsStringAsync();
        var createdUser = JsonSerializer.Deserialize<User>(responseContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(createdUser);
        Assert.Equal(newUser.Name, createdUser.Name);
        Assert.Equal(newUser.Email, createdUser.Email);
        Assert.True(createdUser.Id > 0);
        
        // 驗證資料確實儲存到資料庫
        using var scope = _fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == newUser.Email);
        
        Assert.NotNull(savedUser);
        Assert.Equal(newUser.Name, savedUser.Name);
    }
}

[Collection("WebApi Collection")]
public class ProductsControllerTests
{
    private readonly WebApiFixture _fixture;
    private readonly HttpClient _client;
    
    public ProductsControllerTests(WebApiFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.Client;
    }
    
    [Fact]
    public async Task GetProducts_應回傳產品清單()
    {
        // 這個測試會與 UsersControllerTests 共享同一個 WebApiFixture 執行個體
        // 但每個測試類別仍然有獨立的測試執行個體
        
        // Act
        var response = await _client.GetAsync("/api/products");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    
    [Fact]
    public async Task GetProducts_需要認證_未認證應回傳401()
    {
        // Act
        var response = await _client.GetAsync("/api/products/private");
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task GetProducts_使用認證用戶端_應成功存取()
    {
        // Arrange
        using var authenticatedClient = _fixture.CreateAuthenticatedClient("admin-user");
        
        // Act
        var response = await authenticatedClient.GetAsync("/api/products/private");
        
        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<Product[]>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(products);
    }
}
```

上面的例子有使用到 WebApplicationFactory 做整合測試，有關 WebApplicationFactory 與整合測試的部分，之後其他天數的文章裡會做介紹。

### Fixture 的實作使用

#### Fixture 生命週期管理

```csharp
public class DatabaseFixture : IDisposable
{
    public string ConnectionString { get; }
    public AppDbContext DbContext { get; }
    
    public DatabaseFixture()
    {
        // 建立測試專用的資料庫連線
        ConnectionString = CreateTestDatabase();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
            
        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        
        // 初始化測試資料
        SeedTestData();
    }
    
    private void SeedTestData()
    {
        var testUsers = new[]
        {
            new User { Name = "Test User 1", Email = "test1@example.com" },
            new User { Name = "Test User 2", Email = "test2@example.com" }
        };
        
        DbContext.Users.AddRange(testUsers);
        DbContext.SaveChanges();
    }
    
    public void CleanupData()
    {
        // 在需要時清理測試資料
        DbContext.Users.RemoveRange(DbContext.Users);
        DbContext.SaveChanges();
    }
    
    public void Dispose()
    {
        DbContext?.Dispose();
        CleanupTestDatabase();
    }
    
    private string CreateTestDatabase()
    {
        var dbName = $"TestDb_{Guid.NewGuid():N}";
        return $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=true";
    }
    
    private void CleanupTestDatabase()
    {
        // 清理測試資料庫的邏輯
    }
}
```

#### Fixture 與相依性注入整合

```csharp
public class ServiceFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    
    public ServiceFixture()
    {
        var services = new ServiceCollection();
        
        // 註冊測試所需的服務
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, MockEmailService>();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        // 建立服務提供者
        ServiceProvider = services.BuildServiceProvider();
        
        // 初始化資料庫
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();
        SeedData(dbContext);
    }
    
    private void SeedData(AppDbContext dbContext)
    {
        // 插入測試資料
    }
    
    public T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }
    
    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

public class ServiceIntegrationTests : IClassFixture<ServiceFixture>
{
    private readonly ServiceFixture _fixture;
    
    public ServiceIntegrationTests(ServiceFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public void UserService_建立使用者_應發送歡迎郵件()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>() as MockEmailService;
        
        var newUser = new User { Name = "Test User", Email = "test@example.com" };
        
        // Act
        userService.CreateUser(newUser);
        
        // Assert
        Assert.Single(emailService.SentEmails);
        Assert.Contains("歡迎", emailService.SentEmails.First().Subject);
    }
}
```

---

## xUnit 並行執行基礎

### 理解 xUnit 的並行執行機制

xUnit 預設會嘗試並行執行測試以提升效率，但有一些重要的規則需要了解：

#### 並行執行的層級

1. **不同測試類別**：預設可並行執行
2. **相同測試類別內的方法**：依序執行（不並行）
3. **Collection 內的類別**：依序執行（不並行）

```csharp
// 預設情況：這兩個類別的測試可能會並行執行
public class UserServiceTests
{
    [Fact]
    public void Test1() { /* 可能與 ProductServiceTests.Test2 並行執行 */ }
}

public class ProductServiceTests  
{
    [Fact]
    public void Test2() { /* 可能與 UserServiceTests.Test1 並行執行 */ }
}
```

#### 使用 Collection 控制並行執行

當測試需要共享資源（如資料庫）時，使用 Collection 確保它們不會並行執行：

```csharp
// 使用相同 Collection 的測試不會並行執行
[Collection("Database Tests")]
public class UserRepositoryTests
{
    [Fact]
    public void Test1() { /* 不會與 ProductRepositoryTests 的測試並行執行 */ }
}

[Collection("Database Tests")]
public class ProductRepositoryTests
{
    [Fact]
    public void Test2() { /* 不會與 UserRepositoryTests 的測試並行執行 */ }
}
```

#### 完全禁用並行執行

對於需要嚴格控制執行順序的測試：

```csharp
[Collection("Sequential Tests")]
public class IntegrationTests
{
    [Fact]
    public void Test_Step1() { }
    
    [Fact] 
    public void Test_Step2() { }
}

[CollectionDefinition("Sequential Tests", DisableParallelization = true)]
public class SequentialCollection : ICollectionFixture<object>
{
    // 此 Collection 中的所有測試將完全依序執行
}
```

### xUnit 設定檔

可以透過 `xunit.runner.json` 檔案調整並行執行行為：

```json
{
  "parallelizeTestCollections": false,
  "maxParallelThreads": 4
}
```

**實用建議**：

- xUnit 預設支援並行，但要不要啟用由你判斷。原則上先維持循序，把穩定性放在執行時間前面
- 整合測試或使用共享資源的測試，一律用 Collection 分組把它們序列化
- 有相依順序的測試使用 DisableParallelization

---

## 測試資料與資源管理策略選擇

前面談了 Theory 進階資料提供、Builder 模式、資料提供者模式與 Fixture 資源管理。什麼時候用哪一種，整理如下：

### 資料提供機制選擇

#### 簡單值測試 → InlineData

```csharp
[Theory]
[InlineData(1, 2, 3)]
[InlineData(-1, 1, 0)]
public void Add_基本運算_應回傳正確結果(int a, int b, int expected)
```

- **適用情境**：基本型別的簡單測試
- **優點**：編譯時檢查、執行快速、程式碼簡潔
- **限制**：只能使用常數、無法處理複雜物件

#### 動態資料與重用 → MemberData

```csharp
[Theory]
[MemberData(nameof(GetUserTestData))]
public void ValidateUser_不同情境_應回傳正確結果(User user, bool expected)
```

- **適用情境**：需要動態產生資料或在類別內重用
- **優點**：可動態產生、支援複雜物件、類別內重用
- **考量**：資料與測試類別耦合

#### 跨類別共享與外部資源 → ClassData

```csharp
[Theory]
[ClassData(typeof(UserValidationTestData))]
public void ProcessUser_複雜情境_應正確處理(User user, ValidationResult expected)
```

- **適用情境**：多個測試類別需要共享相同資料，或需要整合外部資料源
- **優點**：高度重用、可整合檔案/資料庫、獨立維護
- **考量**：增加額外的類別和維護成本

### 資源管理與測試架構選擇

#### 單元測試 → Builder 模式

```csharp
var user = UserBuilder.AUser()
    .WithName("Test User")
    .WithValidEmail()
    .Build();
```

- **適用情境**：單元測試需要靈活建立測試物件
- **優點**：表意清晰、易於維護、支援流暢介面
- **建議**：與 MemberData 結合使用效果更佳

#### 整合測試 → IClassFixture

```csharp
public class UserRepositoryTests : IClassFixture<DatabaseFixture>
{
    // 測試會共享同一個資料庫執行個體
}
```

- **適用情境**：需要真實資源（資料庫、檔案系統）的測試
- **優點**：資源重用、避免重複初始化成本
- **考量**：同一類別的測試會共享狀態

#### 跨類別資源共享 → ICollectionFixture

```csharp
[Collection("WebApi Collection")]
public class UsersControllerTests { }

[Collection("WebApi Collection")]  
public class ProductsControllerTests { }
```

- **適用情境**：多個測試類別需要共享昂貴資源
- **優點**：最大化資源重用、控制並行執行
- **考量**：設定複雜、除錯困難

### 決策流程建議

1. **從最簡單開始**：
   - 基本型別測試 → InlineData
   - 物件測試 → Builder + MemberData

2. **根據複雜度調整**：
   - 需要跨類別重用 → ClassData
   - 需要外部資料源 → ClassData + 檔案/資料庫整合

3. **整合測試考量**：
   - 單一類別需要資源 → IClassFixture
   - 多個類別需要共享 → ICollectionFixture

4. **效能最佳化**：
   - 允許並行執行 → 使用不同 Collection 或無 Collection
   - 需要序列執行 → 相同 Collection 或 DisableParallelization

---

## 今日重點回顧

1. **Theory 進階應用**：MemberData、ClassData、PropertyData 的選擇與實作策略
2. **測試資料管理**：Builder 模式、資料提供者模式、分層資料架構
3. **Fixture 深度應用**：IClassFixture、ICollectionFixture 的進階實踐
4. **效能最佳化實務**：並行執行原理、資源池化、執行時間監控
5. **進階實踐**：團隊協作規範、測試組織結構、最佳實務檢查清單

---

## 明日預告

明天我們將介紹**AwesomeAssertions 測試斷言工具深度應用**，包括：

- FluentAssertions 商業授權變化的應對策略
- AwesomeAssertions 作為替代方案的完整評估
- 物件比對與驗證的進階技巧
- 自訂斷言擴充方法的開發實務

---

## 參考資源

### xUnit 進階資源

- **xUnit 官方文件**：[https://xunit.net/docs/shared-context](https://xunit.net/docs/shared-context)
- **xUnit `並行`執行指南**：[https://xunit.net/docs/running-tests-in-parallel](https://xunit.net/docs/running-tests-in-parallel)
- **xUnit 設定參考**：[https://xunit.net/docs/configuration-files](https://xunit.net/docs/configuration-files)

### 測試模式 - Test Data Builder 模式

- **Test Data Builder 模式**：[http://www.natpryce.com/articles/000714.html](http://www.natpryce.com/articles/000714.html)

---

## 老派工程師的心得

本篇涵蓋的 xUnit 功能各自處理不同問題，重點是依測試生命週期、資料來源與隔離需求選用。

**好的測試架構就像好的軟體架構一樣，需要前期投資，但會在長期帶來巨大回報。**

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day03>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第三天。明天會介紹 Day 04：Assertion 工具的深度應用與選擇策略。**
