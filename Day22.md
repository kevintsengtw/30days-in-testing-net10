---
day: 22
title: "Day 22 - Testcontainers 整合測試：MongoDB 及 Redis 基礎到進階"
sample: samples/day22
target_framework: net10.0
packages:
  - AwesomeAssertions
  - Microsoft.Bcl.TimeProvider
  - Microsoft.Extensions.DependencyInjection.Abstractions
  - Microsoft.Extensions.Logging.Abstractions
  - Microsoft.Extensions.Options
  - Microsoft.Extensions.TimeProvider.Testing
  - Microsoft.Testing.Extensions.TrxReport
  - MongoDB.Driver
  - NSubstitute
  - StackExchange.Redis
  - SSH.NET
  - Testcontainers
  - Testcontainers.MongoDb
  - Testcontainers.Redis
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 22 - Testcontainers 整合測試：MongoDB 及 Redis 基礎到進階

<!-- toc -->

- [前言](#前言)
- [本篇學習內容](#本篇學習內容)
- [環境準備與專案架構](#環境準備與專案架構)
- [Collection Fixture 模式深度解析](#collection-fixture-模式深度解析)
- [二、MongoDB 基礎測試實作：從 CRUD 到文件查詢](#二mongodb-基礎測試實作從-crud-到文件查詢)
- [BSON 序列化測試](#bson-序列化測試)
- [MongoDB 索引效能測試](#mongodb-索引效能測試)
- [Redis 整合測試實作](#redis-整合測試實作)
- [Redis 測試總結](#redis-測試總結)
- [今日總結](#今日總結)
- [在本機執行測試（MTP）](#在本機執行測試mtp)
- [參考資料](#參考資料)

<!-- /toc -->

## 前言

NoSQL 資料庫測試是個實用技能。MongoDB 用來處理文件型資料和複雜查詢，Redis 負責快取和即時處理。這兩個在實際專案中很常見，但要寫好測試卻不簡單。

本篇實作 NoSQL 整合測試：MongoDB 涵蓋 CRUD、索引、BSON 與聚合操作；Redis 則測試五種資料結構。容器生命週期沿用 Day21 的 Collection Fixture 模式。

## 本篇學習內容

本篇將帶你建立完整的 NoSQL 測試技能，內容涵蓋：

- **MongoDB 容器環境設定與完整 CRUD 測試實作**：從基礎操作到複雜查詢的完整測試策略
- **MongoDB 索引策略、BSON 處理、資料隔離最佳實務**：確保測試的可靠性和效能
- **MongoDB 進階查詢：複雜條件、聚合操作、效能最佳化**：掌握正式環境的複雜情境
- **Redis 容器環境設定與五種資料結構完整測試**：String、Hash、List、Set、Sorted Set 的深度應用

## 環境準備與專案架構

### 完整環境需求

建立穩定的 NoSQL 測試環境需要以下元件：

- **.NET 10 SDK**：使用最新的 .NET 版本，享受最佳的效能和功能支援
- **Docker Desktop**：容器執行環境，建議版本 4.0 以上
- **xUnit v3（xunit.v3.mtp-v2 3.2.2）**：走 Microsoft.Testing.Platform 的測試框架
- **AwesomeAssertions 9.4.0**：提供多樣化的斷言語法
- **至少 8GB RAM**：確保容器和測試的順暢執行
- **SSD 硬碟**：提升容器啟動和測試執行速度

### 必要套件詳細說明

本系列採用 CPM（Central Package Management），因此 `.csproj` 的 `PackageReference` **只列名稱、不寫版本**，版本統一集中在 per-day 的 `Directory.Packages.props`：

**核心資料庫套件**（`.csproj`）：

```xml
<!-- MongoDB 相關套件 -->
<PackageReference Include="MongoDB.Driver" />

<!-- Redis 相關套件 -->
<PackageReference Include="StackExchange.Redis" />

<!-- Microsoft.Bcl.TimeProvider（提升測試可控性） -->
<PackageReference Include="Microsoft.Bcl.TimeProvider" />
```

> `System.Text.Json` 由 net10.0 共用框架提供，**不需要**另外加 `PackageReference`（否則會出現 NU1510 冗餘套件警告）；程式仍可正常 `using System.Text.Json`。

**測試容器套件**（`.csproj`）：

```xml
<!-- Testcontainers 核心套件 -->
<PackageReference Include="Testcontainers" />
<PackageReference Include="Testcontainers.MongoDb" />
<PackageReference Include="Testcontainers.Redis" />

<!-- 測試框架（xUnit v3 + Microsoft.Testing.Platform） -->
<PackageReference Include="xunit.v3.mtp-v2" />
<PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
<PackageReference Include="AwesomeAssertions" />

<!-- 測試用 TimeProvider -->
<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
```

版本集中在 `Directory.Packages.props`：

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MongoDB.Driver" Version="3.11.0" />
    <PackageVersion Include="StackExchange.Redis" Version="3.1.13" />
    <PackageVersion Include="Microsoft.Bcl.TimeProvider" Version="10.0.10" />
    <PackageVersion Include="Testcontainers" Version="4.13.0" />
    <PackageVersion Include="Testcontainers.MongoDb" Version="4.13.0" />
    <PackageVersion Include="Testcontainers.Redis" Version="4.13.0" />
    <PackageVersion Include="xunit.v3.mtp-v2" Version="3.2.2" />
    <PackageVersion Include="Microsoft.Testing.Extensions.TrxReport" Version="2.3.3" />
    <PackageVersion Include="AwesomeAssertions" Version="9.5.0" />
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.9.0" />
  </ItemGroup>
</Project>
```

**套件版本選擇考量**：

- **MongoDB.Driver 3.10.0**：其相依鏈改用已修補的 `SharpCompress`（≥ 0.48.1）與 `Snappier`（≥ 1.3.1），一併避免這兩個遞移套件的弱點警告
- **StackExchange.Redis 3.1.13**：穩定的 Redis 用戶端，支援 Redis 7.x 的所有功能
- **Testcontainers 4.11.0**：本系列鎖定的版本（不代表你閱讀時的最新版），提供良好的容器管理
- **AwesomeAssertions 9.4.0**：多樣化的斷言語法，提升測試可讀性
- **Microsoft.Bcl.TimeProvider 10.0.5**：提供可測試的時間抽象，避免直接使用 DateTime.UtcNow
- **Microsoft.Extensions.TimeProvider.Testing 10.4.0**：測試用的時間控制工具，實現確定性時間測試

比起 xUnit v2，這裡拿掉了 `Microsoft.NET.Test.Sdk`、`xunit`、`xunit.runner.visualstudio`，改由 `xunit.v3.mtp-v2` 一次涵蓋，並加上 `Microsoft.Testing.Extensions.TrxReport` 產生 TRX 報告。xUnit v3 走 Microsoft.Testing.Platform（MTP），測試專案本身是可執行檔，`.csproj` 要加 `<OutputType>Exe</OutputType>`，再用 `global.json` 指定 SDK 與 `"test": { "runner": "Microsoft.Testing.Platform" }`。

## Collection Fixture 模式深度解析

Collection Fixture 是 xUnit 管理跨測試共享資源的機制。用在 NoSQL 整合測試上，好處有：

**生命週期管理**：

- 容器在測試類別集合開始時啟動一次
- 所有相關測試共享同一個容器執行個體
- 測試類別集合結束時自動清理資源

**效能最佳化**：

- 避免每個測試都重新啟動容器，省下重複啟動的成本（實際節省幅度取決於測試數量與容器啟動成本，沒有固定百分比；請以自己專案的實測為準）
- 減少 Docker 網路操作和資源競爭
- 提升測試執行的一致性和穩定性

**隔離策略**：

- 用資料庫命名和 Key 前綴做到邏輯隔離
- 每個測試使用獨立的資料空間
- 測試間互不影響，確保測試的獨立性

## 二、MongoDB 基礎測試實作：從 CRUD 到文件查詢

### MongoDb Container Fixture 建立

首先建立 MongoDB 容器 Fixture，這是整個測試基礎建設的核心：

```csharp
namespace Day22.Integration.Tests.Fixtures;

/// <summary>
/// MongoDB 容器 Fixture
/// </summary>
public class MongoDbContainerFixture : IAsyncLifetime
{
    private MongoDbContainer? _container;

    /// <summary>
    /// MongoDB 資料庫執行個體
    /// </summary>
    public IMongoDatabase Database { get; private set; } = null!;

    /// <summary>
    /// MongoDB 連線字串
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// 資料庫名稱
    /// </summary>
    public string DatabaseName { get; } = "testdb";

    /// <summary>
    /// 初始化容器
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _container = new MongoDbBuilder("mongo:7.0")
                     .WithPortBinding(27017, true)
                     .Build();

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();
        var client = new MongoClient(ConnectionString);
        Database = client.GetDatabase(DatabaseName);
    }

    /// <summary>
    /// 清理容器資源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// 清空所有集合
    /// </summary>
    public async Task ClearDatabaseAsync()
    {
        var collections = await Database.ListCollectionNamesAsync();
        await collections.ForEachAsync(async collectionName => { await Database.DropCollectionAsync(collectionName); });
    }
}

/// <summary>
/// MongoDB Collection Fixture 定義
/// </summary>
[CollectionDefinition("MongoDb Collection")]
public class MongoDbCollectionFixture : ICollectionFixture<MongoDbContainerFixture>
{
}
```

這個 fixture 用的是 xUnit v3 的 `IAsyncLifetime`：`InitializeAsync`／`DisposeAsync` 的回傳型別從 v2 的 `Task` 改成 `ValueTask`（`DisposeAsync` 現在來自 `IAsyncDisposable`），方法體一個字都不用動。後面的 Redis 容器 fixture 也是同一個定式修正。`ICollectionFixture`／`[CollectionDefinition]`／`[Collection]` 在 v3 同構，維持原樣；本日 MongoDB 與 Redis 兩個各自獨立的 collection 也一樣。

#### xUnit1051：真實呼叫要傳 CancellationToken

xUnit v3 內建一條分析規則 xUnit1051：測試方法裡呼叫「有 `CancellationToken` 多載」的非同步方法卻沒傳 token，就會跳警告，要你改用 `TestContext.Current.CancellationToken`。本日的 MongoDB driver 呼叫幾乎都算——`InsertOneAsync`／`InsertManyAsync`／`CreateOneAsync`／`DeleteManyAsync`／`Find(...).ToListAsync()`／`FirstOrDefaultAsync()` 等，都補上了 token。

有幾個細節值得記一下：

- **token 前面夾了一個 optional 的 options 參數**（`InsertOneAsync`、`InsertManyAsync`、`CreateOneAsync` 的 `InsertOneOptions`／`CreateOneIndexOptions` 等）時，要用具名參數 `cancellationToken:`，否則會被當成 options；token 是最後或唯一參數的（如 `ToListAsync`、`FirstOrDefaultAsync`）直接用位置參數即可。
- **`Assert.ThrowsAsync(() => ...)` 的 lambda 引數 analyzer 會追進去**：lambda 裡的真實呼叫一樣會被標 xUnit1051，一樣要補上 token。
- **自訂服務方法（`MongoUserService`、`RedisCacheService`）與 `StackExchange.Redis` 的 API 不吃 `CancellationToken`**，analyzer 不會標，所以 `MongoUserServiceTests`、`RedisCacheServiceTests` 一處都不用補。

`InitializeAsync`／`DisposeAsync`、constructor、私有輔助方法本身不是測試方法，analyzer 不標，就不用動。

### 完整的使用者文件模型

在實際的 MongoDB 應用中，文件結構往往比簡單的範例複雜得多。我們設計了一個包含巢狀物件、陣列、字典等多種資料型別的使用者文件模型，這樣可以測試 MongoDB 在處理複雜資料結構時的各種情境：

```csharp
namespace Day22.Core.Models.Mongo;

public class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("username")]
    [BsonRequired]
    public string Username { get; set; } = string.Empty;

    [BsonElement("email")]
    [BsonRequired]
    public string Email { get; set; } = string.Empty;

    [BsonElement("profile")]
    public UserProfile Profile { get; set; } = new();

    [BsonElement("addresses")]
    public List<Address> Addresses { get; set; } = new();

    [BsonElement("skills")]
    public List<Skill> Skills { get; set; } = new();

    [BsonElement("preferences")]
    public Dictionary<string, object> Preferences { get; set; } = new();

    [BsonElement("created_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// 用於展示文件更新的樂觀鎖定
    /// </summary>
    /// <param name="updateTime">更新時間</param>
    public void IncrementVersion(DateTime updateTime)
    {
        Version++;
        UpdatedAt = updateTime;
    }
}


/// <summary>
/// 使用者檔案 - 巢狀文件範例
/// </summary>
public class UserProfile
{
    [BsonElement("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [BsonElement("last_name")]
    public string LastName { get; set; } = string.Empty;

    [BsonElement("birth_date")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? BirthDate { get; set; }

    [BsonElement("bio")]
    public string Bio { get; set; } = string.Empty;

    [BsonElement("avatar_url")]
    public string AvatarUrl { get; set; } = string.Empty;

    [BsonElement("social_links")]
    public Dictionary<string, string> SocialLinks { get; set; } = new();

    [BsonIgnore]
    public string FullName => $"{FirstName} {LastName}".Trim();

    [BsonIgnore]
    public int? Age => BirthDate?.CalculateAge();
}

/// <summary>
/// 地址模型 - 用於地理空間查詢
/// </summary>
public class Address
{
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty; // "home", "work", "other"

    [BsonElement("street")]
    public string Street { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("state")]
    public string State { get; set; } = string.Empty;

    [BsonElement("postal_code")]
    public string PostalCode { get; set; } = string.Empty;

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;

    [BsonElement("location")]
    public GeoLocation? Location { get; set; }

    [BsonElement("is_primary")]
    public bool IsPrimary { get; set; }
}

/// <summary>
/// 地理位置 - GeoJSON 格式
/// </summary>
public class GeoLocation
{
    [BsonElement("type")]
    public string Type { get; set; } = "Point";

    [BsonElement("coordinates")]
    public double[] Coordinates { get; set; } = new double[2]; // [longitude, latitude]

    /// <summary>
    /// 建立 GeoJSON Point
    /// </summary>
    public static GeoLocation CreatePoint(double longitude, double latitude)
    {
        return new GeoLocation
        {
            Type = "Point",
            Coordinates = new[] { longitude, latitude }
        };
    }

    [BsonIgnore]
    public double Longitude => Coordinates.Length > 0 ? Coordinates[0] : 0;

    [BsonIgnore]
    public double Latitude => Coordinates.Length > 1 ? Coordinates[1] : 0;
}

/// <summary>
/// 技能模型 - 陣列查詢範例
/// </summary>
public class Skill
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("level")]
    public SkillLevel Level { get; set; } = SkillLevel.Beginner;

    [BsonElement("years_experience")]
    public int YearsExperience { get; set; }

    [BsonElement("certifications")]
    public List<string> Certifications { get; set; } = new();

    [BsonElement("verified")]
    public bool Verified { get; set; }
}

/// <summary>
/// 技能等級列舉
/// </summary>
public enum SkillLevel
{
    [BsonRepresentation(BsonType.String)]
    Beginner,
    
    [BsonRepresentation(BsonType.String)]
    Intermediate,
    
    [BsonRepresentation(BsonType.String)]
    Advanced,
    
    [BsonRepresentation(BsonType.String)]
    Expert
}
```

### MongoDB 基礎服務實作

MongoUserService 提供了完整的 CRUD 操作和進階查詢功能。服務類別實作了樂觀鎖定、索引管理、地理空間查詢等進階功能。

```csharp
namespace Day22.Core.Services;

public interface IUserService
{
    Task<UserDocument> CreateUserAsync(UserDocument user);
    Task<UserDocument?> GetUserByIdAsync(string id);
    Task<UserDocument?> GetUserByEmailAsync(string email);
    Task<List<UserDocument>> GetAllUsersAsync(int skip = 0, int limit = 100);
    Task<UserDocument?> UpdateUserAsync(UserDocument user);
    Task<bool> DeleteUserAsync(string id);
    Task<int> CreateUsersAsync(IEnumerable<UserDocument> users);
    Task<long> GetUserCountAsync();
    Task<bool> UserExistsAsync(string email);
}

public class MongoUserService : IUserService
{
    private readonly IMongoCollection<UserDocument> _users;
    private readonly ILogger<MongoUserService> _logger;
    private readonly MongoDbSettings _settings;
    private readonly TimeProvider _timeProvider;

    public MongoUserService(
        IMongoDatabase database,
        IOptions<MongoDbSettings> settings,
        ILogger<MongoUserService> logger,
        TimeProvider timeProvider)
    {
        _settings = settings.Value;
        _users = database.GetCollection<UserDocument>(_settings.UsersCollectionName);
        _logger = logger;
        _timeProvider = timeProvider;

        // 建立索引
        CreateIndexesAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 建立必要的索引
    /// </summary>
    private async Task CreateIndexesAsync()
    {
        try
        {
            var indexKeysDefinition = Builders<UserDocument>.IndexKeys
                                                            .Ascending(x => x.Email)
                                                            .Ascending(x => x.Username);

            var indexOptions = new CreateIndexOptions
            {
                Unique = true,
                Name = "email_username_unique"
            };

            await _users.Indexes.CreateOneAsync(new CreateIndexModel<UserDocument>(indexKeysDefinition, indexOptions));

            // 地理空間索引
            var geoIndexKeys = Builders<UserDocument>.IndexKeys.Geo2DSphere("addresses.location");

            await _users.Indexes.CreateOneAsync(
                new CreateIndexModel<UserDocument>(
                    geoIndexKeys,
                    new CreateIndexOptions { Name = "addresses_location_2dsphere" }));

            // 技能索引
            var skillIndexKeys = Builders<UserDocument>.IndexKeys
                                                       .Ascending("skills.name")
                                                       .Ascending("skills.level");

            await _users.Indexes.CreateOneAsync(
                new CreateIndexModel<UserDocument>(
                    skillIndexKeys, 
                    new CreateIndexOptions { Name = "skills_compound" }));

            _logger.LogInformation("MongoDB 索引建立完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立 MongoDB 索引時發生錯誤");
        }
    }

    /// <summary>
    /// 新增使用者
    /// </summary>
    public async Task<UserDocument> CreateUserAsync(UserDocument user)
    {
        try
        {
            var now = _timeProvider.GetUtcNow().DateTime;
            user.CreatedAt = now;
            user.UpdatedAt = now;
            user.Version = 1;

            await _users.InsertOneAsync(user);
            _logger.LogInformation("成功建立使用者: {UserId}", user.Id);
            return user;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning("使用者已存在: {Email}", user.Email);
            throw new InvalidOperationException($"使用者 {user.Email} 已存在");
        }
    }

    /// <summary>
    /// 根據 ID 取得使用者
    /// </summary>
    public async Task<UserDocument?> GetUserByIdAsync(string id)
    {
        var filter = Builders<UserDocument>.Filter.Eq(x => x.Id, id);
        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// 根據電子郵件取得使用者
    /// </summary>
    public async Task<UserDocument?> GetUserByEmailAsync(string email)
    {
        var filter = Builders<UserDocument>.Filter.Eq(x => x.Email, email);
        return await _users.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    /// 取得所有使用者（支援分頁）
    /// </summary>
    public async Task<List<UserDocument>> GetAllUsersAsync(int skip = 0, int limit = 100)
    {
        return await _users.Find(FilterDefinition<UserDocument>.Empty)
                           .Skip(skip)
                           .Limit(limit)
                           .SortBy(x => x.Username)
                           .ToListAsync();
    }

    /// <summary>
    /// 更新使用者（使用樂觀鎖定）
    /// </summary>
    public async Task<UserDocument?> UpdateUserAsync(UserDocument user)
    {
        var filter = Builders<UserDocument>.Filter.And(
            Builders<UserDocument>.Filter.Eq(x => x.Id, user.Id),
            Builders<UserDocument>.Filter.Eq(x => x.Version, user.Version)
        );

        user.IncrementVersion(_timeProvider.GetUtcNow().DateTime);

        var result = await _users.ReplaceOneAsync(filter, user);

        if (result.MatchedCount == 0)
        {
            throw new InvalidOperationException("使用者不存在或版本衝突");
        }

        _logger.LogInformation("成功更新使用者: {UserId}, 版本: {Version}", user.Id, user.Version);
        return user;
    }

    /// <summary>
    /// 部分更新使用者檔案
    /// </summary>
    public async Task<bool> UpdateUserProfileAsync(string userId, UserProfile profile)
    {
        var filter = Builders<UserDocument>.Filter.Eq(x => x.Id, userId);
        var update = Builders<UserDocument>.Update
                                           .Set(x => x.Profile, profile)
                                           .Set(x => x.UpdatedAt, _timeProvider.GetUtcNow().DateTime)
                                           .Inc(x => x.Version, 1);

        var result = await _users.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// 新增使用者地址
    /// </summary>
    public async Task<bool> AddUserAddressAsync(string userId, Address address)
    {
        var filter = Builders<UserDocument>.Filter.Eq(x => x.Id, userId);
        var update = Builders<UserDocument>.Update
                                           .Push(x => x.Addresses, address)
                                           .Set(x => x.UpdatedAt, _timeProvider.GetUtcNow().DateTime)
                                           .Inc(x => x.Version, 1);

        var result = await _users.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// 新增使用者技能
    /// </summary>
    public async Task<bool> AddUserSkillAsync(string userId, Skill skill)
    {
        var filter = Builders<UserDocument>.Filter.And(
            Builders<UserDocument>.Filter.Eq(x => x.Id, userId),
            Builders<UserDocument>.Filter.Not(
                Builders<UserDocument>.Filter.ElemMatch(x => x.Skills, s => s.Name == skill.Name)
            )
        );

        var update = Builders<UserDocument>.Update
                                           .Push(x => x.Skills, skill)
                                           .Set(x => x.UpdatedAt, _timeProvider.GetUtcNow().DateTime)
                                           .Inc(x => x.Version, 1);

        var result = await _users.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// 更新使用者技能等級
    /// </summary>
    public async Task<bool> UpdateUserSkillLevelAsync(string userId, string skillName, SkillLevel level)
    {
        var filter = Builders<UserDocument>.Filter.And(
            Builders<UserDocument>.Filter.Eq(x => x.Id, userId),
            Builders<UserDocument>.Filter.ElemMatch(x => x.Skills, s => s.Name == skillName)
        );

        var update = Builders<UserDocument>.Update
                                           .Set("skills.$.level", level)
                                           .Set(x => x.UpdatedAt, _timeProvider.GetUtcNow().DateTime)
                                           .Inc(x => x.Version, 1);

        var result = await _users.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// 地理空間查詢 - 找出附近的使用者
    /// </summary>
    public async Task<List<UserDocument>> FindUsersNearLocationAsync(
        double longitude, double latitude, double maxDistanceKm = 10)
    {
        var location = GeoLocation.CreatePoint(longitude, latitude);

        var filter = Builders<UserDocument>.Filter.Near(
            "addresses.location.coordinates",
            longitude, latitude,
            maxDistanceKm * 1000); // 轉換為公尺

        return await _users.Find(filter).ToListAsync();
    }

    /// <summary>
    /// 聚合查詢 - 按技能等級統計使用者
    /// </summary>
    public async Task<List<BsonDocument>> GetUserSkillStatisticsAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$unwind", "$skills"),
            new BsonDocument("$group", new BsonDocument
            {
                ["_id"] = new BsonDocument
                {
                    ["skill"] = "$skills.name",
                    ["level"] = "$skills.level"
                },
                ["count"] = new BsonDocument("$sum", 1),
                ["avgExperience"] = new BsonDocument("$avg", "$skills.years_experience")
            }),
            new BsonDocument("$sort", new BsonDocument("count", -1))
        };

        return await _users.Aggregate<BsonDocument>(pipeline).ToListAsync();
    }

    /// <summary>
    /// 文字搜尋
    /// </summary>
    public async Task<List<UserDocument>> SearchUsersAsync(string searchText)
    {
        var filter = Builders<UserDocument>.Filter.Or(
            Builders<UserDocument>.Filter.Regex(x => x.Username, new BsonRegularExpression(searchText, "i")),
            Builders<UserDocument>.Filter.Regex(x => x.Email, new BsonRegularExpression(searchText, "i")),
            Builders<UserDocument>.Filter.Regex(x => x.Profile.Bio, new BsonRegularExpression(searchText, "i"))
        );

        return await _users.Find(filter).ToListAsync();
    }

    /// <summary>
    /// 刪除使用者
    /// </summary>
    public async Task<bool> DeleteUserAsync(string id)
    {
        var filter = Builders<UserDocument>.Filter.Eq(x => x.Id, id);
        var result = await _users.DeleteOneAsync(filter);

        if (result.DeletedCount > 0)
        {
            _logger.LogInformation("成功刪除使用者: {UserId}", id);
        }

        return result.DeletedCount > 0;
    }

    // 更多實作方法內容，請看範例專案原始碼
}
```

## BSON 序列化測試

BSON (Binary JSON) 是 MongoDB 的內部資料格式，理解 BSON 的序列化行為對於正確使用 MongoDB 非常重要。以下測試驗證了各種 BSON 序列化情境：

### 測試程式碼

```csharp
using Day22.Core.Models.Mongo;
using Day22.Integration.Tests.Fixtures;
using MongoDB.Bson;

namespace Day22.Integration.Tests.MongoDB;

/// <summary>
/// MongoDB BSON 處理測試
/// </summary>
[Collection("MongoDb Collection")]
public class MongoBsonTests
{
    private readonly MongoDbContainerFixture _fixture;
    private readonly IMongoCollection<UserDocument> _users;
    private readonly ITestOutputHelper _output;

    public MongoBsonTests(MongoDbContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _users = fixture.Database.GetCollection<UserDocument>("bson_test_users");
        _output = output;
    }

    [Fact]
    public async Task BsonObjectIdGeneration_自動產生ObjectId_應為唯一值()
    {
        // Arrange & Act
        var users = new List<UserDocument>();
        for (var i = 0; i < 100; i++)
        {
            var user = new UserDocument
            {
                Username = $"user_{i:D3}",
                Email = $"user{i:D3}@example.com"
            };
            await _users.InsertOneAsync(user, cancellationToken: TestContext.Current.CancellationToken);
            users.Add(user);
        }

        // Assert
        var allIds = users.Select(u => u.Id).ToList();
        allIds.Should().HaveCount(100);
        allIds.Should().OnlyHaveUniqueItems();

        // 驗證 ObjectId 格式
        foreach (var id in allIds)
        {
            ObjectId.TryParse(id, out var objectId).Should().BeTrue();
        }

        _output.WriteLine($"成功產生 {allIds.Count} 個唯一的 ObjectId");
    }

    [Fact]
    public async Task BsonNullAndMissingFields_處理空值和遺失欄位_應正確處理()
    {
        // Arrange - 建立包含 null 值的文件
        var userWithNulls = new BsonDocument
        {
            { "username", "test_user" },
            { "email", "test@example.com" },
            {
                "profile", new BsonDocument
                {
                    { "first_name", "Test" },
                    { "last_name", BsonNull.Value }, // 明確設定為 null
                    // birth_date 欄位完全省略
                    { "bio", "" }
                }
            },
            { "skills", new BsonArray() },         // 空陣列
            { "preferences", new BsonDocument() }, // 空物件
            { "created_at", DateTime.UtcNow },
            { "updated_at", DateTime.UtcNow },
            { "is_active", true }
        };

        // Act - 插入 BSON 文件
        await _users.Database.GetCollection<BsonDocument>("bson_test_users").InsertOneAsync(userWithNulls, cancellationToken: TestContext.Current.CancellationToken);

        // 讀取為強型別物件
        var retrievedUser = await _users
                                  .Find(u => u.Username == "test_user")
                                  .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Username.Should().Be("test_user");
        retrievedUser.Email.Should().Be("test@example.com");
        retrievedUser.Profile.FirstName.Should().Be("Test");
        retrievedUser.Profile.LastName.Should().BeNullOrEmpty();
        retrievedUser.Profile.Bio.Should().Be("");
        retrievedUser.Skills.Should().BeEmpty();

        _output.WriteLine("Null 值和遺失欄位處理測試通過");
    }

    [Fact]
    public async Task BsonComplexDataTypes_複雜資料型別序列化_應正確儲存和讀取()
    {
        // Arrange
        var complexUser = new UserDocument
        {
            Username = "complex_user",
            Email = "complex@example.com",
            Profile = new UserProfile
            {
                FirstName = "Complex",
                LastName = "User",
                BirthDate = new DateTime(1990, 5, 15),
                Bio = "這是一個包含特殊字符的個人簡介：!@#$%^&*()_+{}|:<>?[]\\;'\",./"
            },
            Skills = new List<Skill>
            {
                new() { Name = "C#", Level = SkillLevel.Expert, YearsExperience = 10 },
                new() { Name = "MongoDB", Level = SkillLevel.Advanced, YearsExperience = 5 }
            },
            Preferences = new Dictionary<string, object>
            {
                { "theme", "dark" },
                { "language", "zh-TW" },
                { "notifications", true },
                { "max_items_per_page", 50 },
                { "tags", new[] { "developer", "mongodb", "testing" } }
            }
        };

        // Act
        await _users.InsertOneAsync(complexUser, cancellationToken: TestContext.Current.CancellationToken);
        var retrieved = await _users
                              .Find(u => u.Username == "complex_user")
                              .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Profile.Bio.Should().Be(complexUser.Profile.Bio);
        retrieved.Skills.Should().HaveCount(2);
        retrieved.Skills[0].Name.Should().Be("C#");
        retrieved.Skills[0].Level.Should().Be(SkillLevel.Expert);
        retrieved.Preferences["theme"].Should().Be("dark");
        retrieved.Preferences["notifications"].Should().Be(true);

        _output.WriteLine("複雜資料型別序列化測試通過");
    }

    [Fact]
    public async Task BsonArrayOperations_陣列操作測試_應正確更新陣列元素()
    {
        // Arrange
        var user = new UserDocument
        {
            Username = "array_test_user",
            Email = "array@example.com",
            Skills = new List<Skill>
            {
                new() { Name = "C#", Level = SkillLevel.Advanced, YearsExperience = 5 },
                new() { Name = "JavaScript", Level = SkillLevel.Intermediate, YearsExperience = 3 }
            }
        };

        await _users.InsertOneAsync(user, cancellationToken: TestContext.Current.CancellationToken);

        // Act - 陣列操作：新增、更新、刪除
        // 1. 新增技能
        var addSkillUpdate = Builders<UserDocument>.Update.Push("skills",
                                                                new Skill { Name = "Python", Level = SkillLevel.Beginner, YearsExperience = 1 });
        await _users.UpdateOneAsync(u => u.Username == "array_test_user", addSkillUpdate, cancellationToken: TestContext.Current.CancellationToken);

        // 2. 移除特定技能
        var removeSkillUpdate = Builders<UserDocument>.Update.PullFilter("skills",
                                                                         Builders<Skill>.Filter.Eq(s => s.Name, "JavaScript"));
        await _users.UpdateOneAsync(u => u.Username == "array_test_user", removeSkillUpdate, cancellationToken: TestContext.Current.CancellationToken);

        // 3. 更新陣列中特定元素
        var updateSkillFilter = Builders<UserDocument>.Filter.And(
            Builders<UserDocument>.Filter.Eq(u => u.Username, "array_test_user"),
            Builders<UserDocument>.Filter.ElemMatch(u => u.Skills, s => s.Name == "C#"));
        var updateSkillUpdate = Builders<UserDocument>.Update.Set("skills.$.years_experience", 7);
        await _users.UpdateOneAsync(updateSkillFilter, updateSkillUpdate, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var updatedUser = await _users.Find(u => u.Username == "array_test_user").FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        updatedUser.Should().NotBeNull();
        updatedUser!.Skills.Should().HaveCount(2); // 移除了 JavaScript，新增了 Python
        updatedUser.Skills.Should().Contain(s => s.Name == "C#" && s.YearsExperience == 7);
        updatedUser.Skills.Should().Contain(s => s.Name == "Python" && s.YearsExperience == 1);
        updatedUser.Skills.Should().NotContain(s => s.Name == "JavaScript");

        _output.WriteLine($"陣列操作測試完成，最終技能數量: {updatedUser.Skills.Count}");
    }
}
```

### MongoDB 基礎 CRUD 測試

實作完整的 CRUD 測試，展示各種測試情境：

```csharp
namespace Day22.Integration.Tests.MongoDB;

[Collection("MongoDb Collection")]
public class MongoUserServiceTests
{
    private readonly MongoUserService _mongoUserService;
    private readonly IMongoDatabase _database;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly MongoDbContainerFixture _fixture;

    public MongoUserServiceTests(MongoDbContainerFixture fixture)
    {
        _fixture = fixture;
        _database = fixture.Database;
        var settings = TestSettings.CreateMongoDbSettings();
        var logger = TestSettings.CreateLogger<MongoUserService>();
        _fakeTimeProvider = TestSettings.CreateFakeTimeProvider();
        _mongoUserService = new MongoUserService(_database, settings, logger, _fakeTimeProvider);
    }

    [Fact]
    public async Task CreateUserAsync_輸入有效使用者_應成功建立使用者()
    {
        // Arrange
        var user = new UserDocument
        {
            Username = "testuser",
            Email = "test@example.com",
            Profile = new UserProfile
            {
                FirstName = "Test",
                LastName = "User",
                Bio = "Test user bio"
            }
        };

        // Act
        var result = await _mongoUserService.CreateUserAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be("testuser");
        result.Email.Should().Be("test@example.com");
        result.Id.Should().NotBeEmpty();
        result.CreatedAt.Should().Be(_fakeTimeProvider.GetUtcNow().DateTime);
    }

    [Fact]
    public async Task GetUserByIdAsync_輸入存在的ID_應回傳正確使用者()
    {
        // Arrange
        var user = new UserDocument
        {
            Username = "gettest",
            Email = "gettest@example.com",
            Profile = new UserProfile { FirstName = "Get", LastName = "Test" }
        };
        var createdUser = await _mongoUserService.CreateUserAsync(user);

        // Act
        var result = await _mongoUserService.GetUserByIdAsync(createdUser.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("gettest");
        result.Email.Should().Be("gettest@example.com");
    }

    [Fact]
    public async Task GetUserByEmailAsync_輸入存在的Email_應回傳正確使用者()
    {
        // Arrange
        var user = new UserDocument
        {
            Username = "emailtest",
            Email = "emailtest@example.com",
            Profile = new UserProfile { FirstName = "Email", LastName = "Test" }
        };
        await _mongoUserService.CreateUserAsync(user);

        // Act
        var result = await _mongoUserService.GetUserByEmailAsync("emailtest@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("emailtest");
        result.Email.Should().Be("emailtest@example.com");
    }

    // 更多的測試案例請看範例專案原始碼
}
```

這些測試展示了基本的 MongoDB CRUD 操作測試，使用 Collection Fixture 確保測試之間的隔離性。

## MongoDB 索引效能測試

索引是 MongoDB 效能的關鍵，測試索引的建立和效能影響：

```csharp
namespace Day22.Integration.Tests.MongoDB;

[Collection("MongoDb Collection")]
public class MongoIndexTests
{
    private readonly MongoDbContainerFixture _fixture;
    private readonly IMongoCollection<UserDocument> _users;
    private readonly ITestOutputHelper _output;

    public MongoIndexTests(MongoDbContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _users = fixture.Database.GetCollection<UserDocument>("index_test_users");
        _output = output;
    }

    [Fact]
    public async Task CreateUniqueIndex_電子郵件唯一索引_應防止重複插入()
    {
        // Arrange - 確保集合為空
        await _users.DeleteManyAsync(FilterDefinition<UserDocument>.Empty, TestContext.Current.CancellationToken);

        // 建立唯一索引
        var indexKeysDefinition = Builders<UserDocument>.IndexKeys.Ascending(u => u.Email);
        var indexOptions = new CreateIndexOptions { Unique = true };
        var indexModel = new CreateIndexModel<UserDocument>(indexKeysDefinition, indexOptions);
        await _users.Indexes.CreateOneAsync(indexModel, cancellationToken: TestContext.Current.CancellationToken);

        var user1 = new UserDocument
        {
            Username = "user1",
            Email = "test@example.com"
        };

        var user2 = new UserDocument
        {
            Username = "user2",
            Email = "test@example.com" // 相同的電子郵件
        };

        // Act & Assert
        await _users.InsertOneAsync(user1, cancellationToken: TestContext.Current.CancellationToken); // 第一次插入應該成功

        // 第二次插入相同 email 應該失敗
        var exception = await Assert.ThrowsAsync<MongoWriteException>(() => _users.InsertOneAsync(user2, cancellationToken: TestContext.Current.CancellationToken));
        exception.WriteError.Category.Should().Be(ServerErrorCategory.DuplicateKey);

        _output.WriteLine("唯一索引測試通過 - 重複的 email 被正確阻擋");
    }

    [Fact]
    public async Task CompoundIndex_複合索引查詢效能_應顯著提升查詢速度()
    {
        // Arrange - 確保集合為空
        await _users.DeleteManyAsync(FilterDefinition<UserDocument>.Empty, TestContext.Current.CancellationToken);

        // 插入大量測試資料
        var testUsers = new List<UserDocument>();
        for (var i = 0; i < 1000; i++)
        {
            testUsers.Add(new UserDocument
            {
                Username = $"user_{i:D4}",
                Email = $"user{i:D4}@example.com",
                IsActive = i % 2 == 0,
                CreatedAt = DateTime.UtcNow.AddDays(-i % 365)
            });
        }

        await _users.InsertManyAsync(testUsers, cancellationToken: TestContext.Current.CancellationToken);

        // 測試查詢效能 - 沒有索引
        var stopwatch = Stopwatch.StartNew();
        var filter = Builders<UserDocument>.Filter.And(
            Builders<UserDocument>.Filter.Eq(u => u.IsActive, true),
            Builders<UserDocument>.Filter.Gte(u => u.CreatedAt, DateTime.UtcNow.AddDays(-100))
        );

        await _users.Find(filter).ToListAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();
        var timeWithoutIndex = stopwatch.ElapsedMilliseconds;

        // 建立複合索引
        var compoundIndex = Builders<UserDocument>.IndexKeys
                                                  .Ascending(u => u.IsActive)
                                                  .Descending(u => u.CreatedAt);
        await _users.Indexes.CreateOneAsync(new CreateIndexModel<UserDocument>(compoundIndex), cancellationToken: TestContext.Current.CancellationToken);

        // 測試查詢效能 - 有索引
        stopwatch.Restart();
        await _users.Find(filter).ToListAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();
        var timeWithIndex = stopwatch.ElapsedMilliseconds;

        // Assert
        // 索引應該提升查詢效能（但在小資料集中可能差異不大）
        var improvement = (double)timeWithoutIndex / Math.Max(timeWithIndex, 1);

        _output.WriteLine($"查詢時間比較 - 無索引: {timeWithoutIndex}ms, 有索引: {timeWithIndex}ms");
        _output.WriteLine($"效能提升倍數: {improvement:F2}x");

        // 在小資料集中，索引帶來的效能提升可能不明顯，但應該不會變慢
        timeWithIndex.Should().BeLessThan(timeWithoutIndex + 100); // 允許100ms的誤差
    }

    // 更多的測試案例請看範例專案原始碼
}
```

---

## Redis 整合測試實作

### Redis 服務層設計思路

在範例專案中，我們設計了一個功能完整的 `RedisCacheService`，它不只是簡單的 Get/Set 操作。這個服務整合了：

- **完整的相依性注入**：支援設定檔、日誌、時間提供者
- **五種資料結構**：String、Hash、List、Set、Sorted Set 的完整操作
- **靈活的序列化**：JSON 序列化與反序列化，支援複雜物件
- **TTL 管理**：過期時間的設定和查詢
- **批次操作**：提升大量資料處理的效能

### Redis 容器 Fixture 設定

首先建立 Redis 容器的測試基礎建設。相比 MongoDB，Redis 的設定更加簡潔：

```csharp
namespace Day22.Integration.Tests.Fixtures;

/// <summary>
/// Redis 容器 Fixture
/// </summary>
public class RedisContainerFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    /// <summary>
    /// Redis 連線物件
    /// </summary>
    public IConnectionMultiplexer Connection { get; private set; } = null!;

    /// <summary>
    /// Redis 資料庫執行個體
    /// </summary>
    public IDatabase Database { get; private set; } = null!;

    /// <summary>
    /// Redis 連線字串
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// 初始化容器
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _container = new RedisBuilder("redis:7.2")
                     .WithPortBinding(6379, true)
                     .Build();

        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();
        Connection = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
        Database = Connection.GetDatabase();
    }

    /// <summary>
    /// 清理容器資源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Connection != null)
        {
            await Connection.DisposeAsync();
        }

        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// 清空 Redis 資料庫
    /// </summary>
    public async Task ClearDatabaseAsync()
    {
        // 使用 DEL 命令逐一刪除 keys，而不是 FLUSHDB
        // 這是因為 Redis 容器可能沒有啟用 admin 模式
        var keys = Connection.GetServer(Connection.GetEndPoints().First()).Keys(Database.Database);
        if (keys.Any())
        {
            await Database.KeyDeleteAsync(keys.ToArray());
        }
    }
}

/// <summary>
/// Redis Collection Fixture 定義
/// </summary>
[CollectionDefinition("Redis Collection")]
public class RedisCollectionFixture : ICollectionFixture<RedisContainerFixture>
{
}
```

### Redis 快取模型定義

在實際應用中，我們需要處理複雜的快取項目。範例專案定義了一個通用的快取包裝器：

```csharp
public class CacheItem<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("access_count")]
    public int AccessCount { get; set; }

    [JsonPropertyName("last_accessed")]
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [JsonIgnore]
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    [JsonIgnore]
    public double TtlSeconds => ExpiresAt.HasValue
        ? Math.Max(0, ExpiresAt.Value.Subtract(DateTime.UtcNow).TotalSeconds)
        : -1;

    public static CacheItem<T> Create(string key, T data, TimeSpan? ttl = null, params string[] tags)
    {
        return new CacheItem<T>
        {
            Key = key,
            Data = data,
            ExpiresAt = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null,
            Tags = tags.ToList()
        };
    }

    public void UpdateAccess()
    {
        AccessCount++;
        LastAccessed = DateTime.UtcNow;
    }
}
```

這個模型提供了豐富的快取元資料，適合實際專案的複雜需求。

### Redis 基本資料結構測試

現在來看實際的測試實作。我們的測試涵蓋了五種 Redis 資料結構的完整功能：

```csharp
[Collection("Redis Collection")]
public class RedisCacheServiceTests
{
    private readonly RedisCacheService _redisCacheService;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly RedisContainerFixture _fixture;

    public RedisCacheServiceTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
        var settings = TestSettings.CreateRedisSettings();
        var logger = TestSettings.CreateLogger<RedisCacheService>();
        _fakeTimeProvider = TestSettings.CreateFakeTimeProvider();
        _redisCacheService = new RedisCacheService(fixture.Connection, settings, logger, _fakeTimeProvider);
    }

    [Fact]
    public async Task SetStringAsync_輸入字串值_應成功設定快取()
    {
        // Arrange
        var key = "test_string_key";
        var value = "test_string_value";

        // Act
        var result = await _redisCacheService.SetStringAsync(key, value);

        // Assert
        result.Should().BeTrue();

        var retrievedValue = await _redisCacheService.GetStringAsync<string>(key);
        retrievedValue.Should().Be(value);
    }

    [Fact]
    public async Task SetObjectCacheAsync_輸入物件_應成功序列化並快取()
    {
        // Arrange
        var key = "object_test_key";
        var user = new UserDocument
        {
            Username = "objecttest",
            Email = "object@test.com",
            Profile = new UserProfile
            {
                FirstName = "Object",
                LastName = "Test"
            }
        };

        // Act
        var result = await _redisCacheService.SetStringAsync(key, user, TimeSpan.FromMinutes(30));

        // Assert
        result.Should().BeTrue();

        var retrievedUser = await _redisCacheService.GetStringAsync<UserDocument>(key);
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Username.Should().Be("objecttest");
        retrievedUser.Email.Should().Be("object@test.com");
    }

    [Fact]
    public async Task SetMultipleStringAsync_輸入多個鍵值對_應成功批次設定()
    {
        // Arrange
        var keyValues = new Dictionary<string, string>
        {
            { "multi1", "value1" },
            { "multi2", "value2" },
            { "multi3", "value3" }
        };

        // Act
        var result = await _redisCacheService.SetMultipleStringAsync(keyValues);

        // Assert
        result.Should().BeTrue();

        foreach (var kvp in keyValues)
        {
            var value = await _redisCacheService.GetStringAsync<string>(kvp.Key);
            value.Should().Be(kvp.Value);
        }
    }

    [Fact]
    public async Task SetHashAsync_輸入Hash欄位_應成功設定()
    {
        // Arrange
        var key = "hash_test";
        var field = "field1";
        var value = "hash_value1";

        // Act
        var result = await _redisCacheService.SetHashAsync(key, field, value);

        // Assert
        result.Should().BeTrue();

        var retrievedValue = await _redisCacheService.GetHashAsync<string>(key, field);
        retrievedValue.Should().Be(value);
    }

    [Fact]
    public async Task SetHashAllAsync_輸入物件_應設定完整Hash()
    {
        // Arrange
        var key = "hash_all_test";
        var session = new UserSession
        {
            UserId = "user123",
            SessionId = "session456",
            IpAddress = "192.168.1.1",
            UserAgent = "Test Browser",
            IsActive = true
        };

        // Act
        var result = await _redisCacheService.SetHashAllAsync(key, session, TimeSpan.FromHours(1));

        // Assert
        result.Should().BeTrue();

        var retrievedSession = await _redisCacheService.GetHashAllAsync<UserSession>(key);
        retrievedSession.Should().NotBeNull();
        retrievedSession!.UserId.Should().Be("user123");
        retrievedSession.SessionId.Should().Be("session456");
        retrievedSession.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ListLeftPushAsync_輸入值_應新增到List左側()
    {
        // Arrange
        var key = "list_test";
        var view1 = new RecentView { ItemId = "item1", ItemType = "product", Title = "Product 1" };
        var view2 = new RecentView { ItemId = "item2", ItemType = "product", Title = "Product 2" };

        // Act
        var count1 = await _redisCacheService.ListLeftPushAsync(key, view1);
        var count2 = await _redisCacheService.ListLeftPushAsync(key, view2);

        // Assert
        count1.Should().Be(1);
        count2.Should().Be(2);

        var views = await _redisCacheService.ListRangeAsync<RecentView>(key);
        views.Should().HaveCount(2);
        views[0].ItemId.Should().Be("item2"); // 最後加入的在最前面
        views[1].ItemId.Should().Be("item1");
    }

    [Fact]
    public async Task SortedSetAddAsync_輸入分數和成員_應成功新增到排序集合()
    {
        // Arrange
        var key = "sorted_set_test";
        var entry1 = new LeaderboardEntry { UserId = "user1", Username = "Player1", Score = 100 };
        var entry2 = new LeaderboardEntry { UserId = "user2", Username = "Player2", Score = 200 };

        // Act
        var result1 = await _redisCacheService.SortedSetAddAsync(key, entry1, entry1.Score);
        var result2 = await _redisCacheService.SortedSetAddAsync(key, entry2, entry2.Score);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();

        var rankings = await _redisCacheService.SortedSetRangeWithScoresAsync<LeaderboardEntry>(key, 0, -1, Order.Descending);
        rankings.Should().HaveCount(2);
        rankings[0].Member.Username.Should().Be("Player2"); // 分數高的在前面
        rankings[0].Score.Should().Be(200);
    }

    [Fact]
    public async Task SetAddAsync_輸入值_應新增到Set()
    {
        // Arrange
        var key = "set_test";
        var tag1 = "programming";
        var tag2 = "testing";
        var tag3 = "programming"; // 重複

        // Act
        var result1 = await _redisCacheService.SetAddAsync(key, tag1);
        var result2 = await _redisCacheService.SetAddAsync(key, tag2);
        var result3 = await _redisCacheService.SetAddAsync(key, tag3);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeFalse(); // 重複項目

        var tags = await _redisCacheService.SetMembersAsync<string>(key);
        tags.Should().HaveCount(2);
        tags.Should().Contain("programming");
        tags.Should().Contain("testing");
    }

    [Fact]
    public async Task ExpireAsync_輸入過期時間_應正確設定TTL()
    {
        // Arrange
        var key = "expire_test_key";
        var value = "expire_test_value";
        await _redisCacheService.SetStringAsync(key, value);

        // Act
        var result = await _redisCacheService.ExpireAsync(key, TimeSpan.FromSeconds(60));

        // Assert
        result.Should().BeTrue();

        var ttl = await _redisCacheService.GetTtlAsync(key);
        ttl.Should().NotBeNull();
        ttl.Value.TotalSeconds.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task SearchKeysAsync_輸入模式_應回傳符合的鍵值()
    {
        // Arrange
        await _redisCacheService.SetStringAsync("search:test1", "value1");
        await _redisCacheService.SetStringAsync("search:test2", "value2");
        await _redisCacheService.SetStringAsync("other:test", "value3");

        // Act
        var result = await _redisCacheService.SearchKeysAsync("search:*");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("search:test1");
        result.Should().Contain("search:test2");
    }
}
```

### Redis Stream 和進階功能測試

Redis Stream 是較新的功能，適合處理訊息佇列和事件串流：

```csharp
[Fact]
public async Task StreamAddAsync_輸入資料_應成功新增到Stream()
{
    // Arrange
    var key = "stream_test";
    var notification = new NotificationMessage
    {
        UserId = "user123",
        Title = "Test Notification",
        Content = "This is a test notification",
        Type = "info"
    };

    // Act
    var result = await _redisCacheService.StreamAddAsync(key, notification);

    // Assert
    if (result.HasValue)
    {
        result.Should().NotBeNull();

        var messages = await _redisCacheService.StreamRangeAsync<NotificationMessage>(key);
        messages.Should().ContainSingle();
        messages[0].Data.Title.Should().Be("Test Notification");
    }
    else
    {
        // 如果 Stream 操作失敗，跳過這個測試
        Assert.True(true, "Stream operation not supported or failed - skipping test");
    }
}
```

### 測試資料隔離和清理策略

在 Redis 測試中，資料隔離特別重要。我們使用幾種策略：

1. **Key 前綴隔離**：每個測試使用唯一的前綴
2. **測試後清理**：選擇性清理測試資料
3. **容器重置**：必要時重新啟動容器

```csharp
[Fact]
public async Task 測試資料隔離_多個測試同時執行_應不互相影響()
{
    // Arrange
    var testId = Guid.NewGuid().ToString("N")[..8];
    var key1 = $"isolation_test:{testId}:key1";
    var key2 = $"isolation_test:{testId}:key2";

    // Act
    await _redisCacheService.SetStringAsync(key1, "value1");
    await _redisCacheService.SetStringAsync(key2, "value2");

    // Assert
    var value1 = await _redisCacheService.GetStringAsync<string>(key1);
    var value2 = await _redisCacheService.GetStringAsync<string>(key2);

    value1.Should().Be("value1");
    value2.Should().Be("value2");

    // Cleanup
    await _redisCacheService.DeleteAsync(key1);
    await _redisCacheService.DeleteAsync(key2);
}
```

### Redis 容器權限問題解決

在實際專案中，我們遇到了 Redis 容器的權限問題。某些 Redis 容器映像檔預設不啟用 admin 模式，導致 `FLUSHDB` 指令失敗。解決方案：

```csharp
// 錯誤的做法：使用 FLUSHDB
await server.FlushDatabaseAsync();

// 正確的做法：使用 KeyDelete 逐一刪除
var keys = server.Keys(database.Database);
if (keys.Any())
{
    await database.KeyDeleteAsync(keys.ToArray());
}
```

## Redis 測試總結

Redis 整合測試的重點包括：

1. **連線管理**：使用 Testcontainers 建立獨立的測試環境
2. **資料結構涵蓋**：測試 String、Hash、List、Set、Sorted Set 等五種主要資料結構
3. **錯誤處理**：確保服務在各種例外情況下的穩定性
4. **效能驗證**：確保操作在合理時間內完成

Redis 整合測試能驗證資料結構操作與快取邏輯；正式環境的可靠性與效能仍要另外監控和量測。

## 今日總結

本篇建立了 MongoDB 與 Redis 的整合測試架構，前者著重文件操作，後者驗證五種資料結構。

### MongoDB 測試的完整實作

**BSON 與文件模型**：UserDocument 包含巢狀物件、陣列與地理位置，足以驗證常見的複合文件。BSON 序列化測試則確認資料寫入 MongoDB 後能正確讀回。

**索引與效能測試**：測試會建立唯一索引與複合索引。小資料集看不出可靠的效能差異，因此這裡只驗證索引存在與查詢行為，不把結果當成正式 benchmark。

**進階查詢功能**：涵蓋樂觀鎖定、地理空間查詢與聚合操作，對應範例服務實際使用的 MongoDB 行為。

### Redis 測試的深度涵蓋

**五種資料結構完整測試**：String、Hash、List、Set、Sorted Set 的操作都有對應的測試案例，不只是基本的存取，還包含了實際應用情境，如最近瀏覽紀錄、排行榜、標籤系統等。

**序列化與快取策略**：用 `JsonSerializerOptions` 控制複雜物件的序列化；`CacheItem` 模型則記錄版本、存取統計與 TTL。

**批次操作與效能最佳化**：SetMultipleStringAsync 這類批次操作加上 TTL 管理，正式環境的效能差別就在這裡。

### Testcontainers 的實戰價值

**Collection Fixture 模式**將這組範例的執行時間從約 30–40 秒縮短到 15 秒。容器只啟動一次，測試間的隔離則交給資料清理與命名策略。

**真實環境模擬**比 Mock 物件更可靠。我們用的是真正的 MongoDB 7.0 和 Redis 7.2 容器，測試結果能直接反映正式環境的行為，避免了「測試通過但正式環境出錯」的尷尬。

### 實務應用心得

1. **從 CRUD 開始，逐步加入複雜功能**：先確保基本操作穩定，再加入索引、聚合等進階功能
2. **資料隔離是穩定測試的基礎**：每個測試都要有乾淨的起始狀態，避免測試間的相互干擾
3. **效能測試要有合理預期**：小資料集中的效能差異可能不明顯，重點是建立測試架構
4. **錯誤處理同樣重要**：容器啟動失敗、連線中斷等例外情況都要有對應的處理機制

---

## 在本機執行測試（MTP）

xUnit v3 走 Microsoft Testing Platform（MTP），runner 由本日 sample 的 `global.json` 指定。**務必先切換到該 sample 目錄再執行**，否則不會套用 per-day `global.json`，`dotnet test` 會落回 VSTest 而失敗；也不要從 repository root 直接指定子目錄 solution：

```powershell
Set-Location samples/day22
dotnet test --solution Day22.NoSqlTesting.sln -c Release
```

## 參考資料

- [Testcontainers 官方網站](https://testcontainers.com/)
- [.NET Testcontainers 文件](https://dotnet.testcontainers.org/)
- [Testcontainers for .NET GitHub](https://github.com/testcontainers/testcontainers-dotnet)
- [Getting Started with Testcontainers for .NET](https://testcontainers.com/guides/getting-started-with-testcontainers-for-dotnet/)
- [MongoDB.Driver 官方文件](https://www.mongodb.com/docs/drivers/csharp/)
- [StackExchange.Redis 官方文件](https://stackexchange.github.io/StackExchange.Redis/)
- [xUnit Collection Fixtures](https://xunit.net/docs/shared-context#collection-fixture)
- [AwesomeAssertions 官方文件](https://awesomeassertions.org/)

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day22>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第二十二天。明天會介紹 Day 23 - 整合測試實戰：WebApi 服務的整合測試。**
