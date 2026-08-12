# Day 15 - AutoFixture 與 Bogus 整合範例專案

這個專案展示了如何將 AutoFixture 和 Bogus 這兩個測試資料產生工具進行整合，以獲得兼具便利性和真實感的測試資料。

## 專案結構

```text
Day15.AutoFixtureBogusMix/
├── src/
│   └── AutoFixtureBogusMix.Core/
│       ├── Models/                     # 領域模型
│       │   └── DomainModels.cs        # User, Order, Company 等實體
│       └── TestData/                   # 測試資料產生相關
│           ├── ITestDataGenerator.cs   # 統一介面
│           ├── HybridTestDataGenerator.cs  # 混合產生器
│           ├── Base/
│           │   └── TestBase.cs        # 測試基底類別
│           ├── Extensions/
│           │   └── FixtureExtensions.cs    # AutoFixture 擴展方法
│           ├── Factories/
│           │   └── IntegratedTestDataFactory.cs  # 整合工廠
│           └── SpecimenBuilders/       # 自訂 SpecimenBuilder
│               ├── PropertySpecimenBuilders.cs   # 屬性級整合
│               └── BogusSpecimenBuilder.cs       # 類型級整合
└── tests/
    └── AutoFixtureBogusMix.Tests/
        ├── IntegratedTestDataTests.cs      # 整合功能測試
        ├── PerformanceTests.cs             # 效能測試
        ├── RealWorldApplicationTests.cs    # 實際應用測試
        └── SeedManagementTests.cs          # 種子管理測試
```

## 主要功能

### 1. 混合測試資料產生器

結合 AutoFixture 的便利性和 Bogus 的真實感：

```csharp
var generator = new HybridTestDataGenerator();
var user = generator.Generate<User>();
// user.Email 將是真實格式的電子郵件
// user.FirstName 將是真實的姓名
```

### 2. AutoFixture 擴展方法

簡潔的 API 讓整合更容易：

```csharp
var fixture = new Fixture().WithBogus();
var company = fixture.Create<Company>();
```

### 3. 整合測試資料工廠

提供進階功能如快取、批次產生等：

```csharp
var factory = new IntegratedTestDataFactory();
var scenario = factory.CreateTestScenario(); // 完整的測試情境
```

### 4. 測試基底類別

統一的測試資料產生功能：

```csharp
public class MyTests : TestBase
{
    [Fact]
    public void SomeTest()
    {
        var user = Create<User>();  // 直接使用
        var orders = CreateMany<Order>(5);
    }
}
```

## 核心優勢

### 真實感資料

- Email: 真實格式的電子郵件地址
- Phone: 符合格式的電話號碼
- Name: 真實的人名
- Address: 真實的地址資訊

### 便利性

- 自動處理複雜物件結構
- 循環參考自動處理
- 簡潔的 API

### 可重現性

- 種子管理確保測試可重現
- 支援固定種子和隨機種子

### 效能最佳化

- 快取機制減少重複產生
- 批次產生最佳化

## 使用範例

### 基本使用

```csharp
// 方法 1: 使用混合產生器
var generator = new HybridTestDataGenerator();
var user = generator.Generate<User>();

// 方法 2: 使用 AutoFixture 擴展
var fixture = new Fixture().WithBogus();
var company = fixture.Create<Company>();

// 方法 3: 使用工廠
var factory = new IntegratedTestDataFactory();
var scenario = factory.CreateTestScenario();
```

### 自訂 Faker

```csharp
var customUserFaker = new Faker<User>()
    .RuleFor(u => u.FirstName, "John")
    .RuleFor(u => u.Age, f => f.Random.Int(25, 65));

var fixture = new Fixture().WithBogusFor(customUserFaker);
var user = fixture.Create<User>();
```

### 種子管理

```csharp
// 使用相同 seed 讓資料格式與結構保持一致
var generator = new HybridTestDataGenerator(seed: 123);
var user1 = generator.Generate<User>();

var generator2 = new HybridTestDataGenerator(seed: 123);
var user2 = generator2.Generate<User>();
// user1 和 user2 會有一致的資料格式與結構；
// 但 AutoFixture 與 Bogus 使用不同的隨機機制，整合後不保證每個欄位的值完全相同。
// 若需要值完全可重現，建議改用單一工具（純 AutoFixture 或純 Bogus 的 Faker<T>.UseSeed）。
```

## 套件相依性

- **AutoFixture**: 匿名測試資料產生
- **Bogus**: 真實感假資料產生
- **AwesomeAssertions**: 測試斷言
- **xUnit v3**: 測試框架（Microsoft Testing Platform 模式）

## 執行測試

```bash
# 建置專案
dotnet build

# 執行所有測試
dotnet test

# 執行特定測試類別
dotnet test --filter-class "AutoFixtureBogusMix.Tests.IntegratedTestDataTests"

# 執行效能測試
dotnet test --filter-class "AutoFixtureBogusMix.Tests.PerformanceTests"
```

## 效能測試設計說明

`PerformanceTests` 的門檻值是刻意調整過的，原因如下：

- `User` 物件圖含循環參考（`User → Company → Employees` 又回到 `List<User>`），雖有 `OmitOnRecursionBehavior` 避免無限迴圈，每個 User 的產生成本仍約 10～50ms（依機器而定）
- 因此複雜物件的大量產生測試把資料量從 1000 降到 100、門檻放寬到 10 秒；1000 筆的大量產生測試改用 `Address` 這類無關聯的簡單物件驗證
- 如果要在自己的專案做類似測試，建議：複雜物件降低數量或改用專用的簡化工廠，大量資料場景用簡單物件衡量

## 學習重點

1. **SpecimenBuilder 整合模式**: 如何自訂 AutoFixture 的產生邏輯
2. **屬性 vs 類型級整合**: 不同層級的整合策略
3. **效能考量**: 快取和批次產生的最佳實務
4. **種子管理**: 確保測試可重現性的重要性
5. **實際應用**: 在真實專案中如何應用這些技術

## 延伸閱讀

- [AutoFixture 官方文件](https://github.com/AutoFixture/AutoFixture)
- [Bogus 官方文件](https://github.com/bchavez/Bogus)
- [Day 15 完整教學文章](../../Day15.md)
