# Day 03 範例專案：xUnit 進階功能與測試資料管理

本專案展示了 `Day03.md` 文章中介紹的 xUnit 進階功能與測試資料管理技巧。

## 專案結構

```
day03/
├── src/
│   └── Day03.Domain/           # 領域模型和服務
│       ├── Models/             # 資料模型
│       ├── Services/           # 業務服務
│       ├── Repositories/       # 資料存取層
│       └── Data/              # EF Core DbContext
└── tests/
    └── Day03.Tests/           # 測試專案
        ├── AdvancedTheoryTests/      # Theory 進階應用測試
        ├── BuilderPatternTests/      # Builder 模式測試
        ├── TestDataProviders/        # 測試資料提供者
        ├── FixtureTests/            # Fixture 資源管理測試
        └── ParallelExecutionTests/   # 平行執行控制測試
```

## 主要功能展示

### 1. Theory 進階資料提供機制

#### MemberData 使用
- `CalculatorAdvancedTests.cs` - 使用靜態屬性提供測試資料
- `StringValidationTests.cs` - 使用靜態方法動態產生測試資料
- `ComplexObjectTests.cs` - 複雜物件的測試資料管理

#### ClassData 使用
- `CalculatorClassDataTests.cs` - 基本 ClassData 實作
- `CsvDataTests.cs` - 整合外部 CSV 檔案的測試資料

#### 跨類別共享測試資料
- `CommonTestData.cs` - 靜態共享測試資料類別

### 2. 測試資料重複使用策略

#### Builder 模式
- `UserBuilder.cs` - 完整的 Builder 模式實作
- `UserServiceTests.cs` - Builder 在測試中的應用
- `UserValidationTests.cs` - Builder 與 MemberData 結合使用

#### 資料提供者模式
- `ITestDataProvider<T>` - 通用資料提供者介面
- `UserTestDataProvider.cs` - 使用者資料提供者實作

### 3. xUnit 進階資源管理

#### IClassFixture
- `DatabaseFixture.cs` - 資料庫資源 Fixture
- `UserRepositoryTests.cs` - 使用 DatabaseFixture 的測試

#### ICollectionFixture
- `ServiceFixture.cs` - 服務層級的資源共享
- `ServiceIntegrationTests.cs` - Collection Fixture 的使用範例

### 4. 平行執行控制

#### 平行執行機制展示
- `DefaultParallelTests.cs` - 預設平行執行行為
- `CollectionBasedTests.cs` - 使用 Collection 控制平行執行
- `SequentialTests.cs` - 完全禁用平行執行

#### xUnit 設定
- `xunit.runner.json` - xUnit 執行器設定檔案

## 如何執行

### 建置專案
```bash
cd samples/day03
dotnet build
```

### 執行所有測試
```bash
dotnet test --solution Day03.sln
```

### 執行特定測試類別（xUnit v3 MTP 篩選語法，`*` 為萬用字元）
```bash
# 執行 Theory 進階測試
dotnet test --solution Day03.sln --filter-class "*AdvancedTheoryTests*"

# 執行 Builder 模式測試
dotnet test --solution Day03.sln --filter-class "*BuilderPatternTests*"

# 執行 Fixture 測試
dotnet test --solution Day03.sln --filter-class "*FixtureTests*"
```

### 輸出測試報告
```bash
# xUnit v3 MTP 原生報告：輸出 TRX
dotnet test --solution Day03.sln --report-trx --report-trx-filename day03.trx
```

## 學習重點

### 1. 資料提供機制選擇指南
- **簡單值測試** → `InlineData`
- **動態資料與重用** → `MemberData`
- **跨類別共享與外部資源** → `ClassData`

### 2. 資源管理策略
- **單元測試** → Builder 模式
- **整合測試** → `IClassFixture`
- **跨類別資源共享** → `ICollectionFixture`

### 3. 平行執行最佳實踐
- 框架支援平行與是否啟用是兩件事：原則上先維持循序，把穩定性放在執行時間前面
- 整合測試或使用共享資源的測試，一律用 Collection 分組序列化
- 有相依順序的測試使用 `DisableParallelization`

## 實際檔案說明

### 測試資料檔案
- `TestData/calculations.csv` - CSV 格式的測試資料範例

### 設定檔案
- `xunit.runner.json` - xUnit 平行執行設定

### 核心測試檔案
每個測試檔案都對應 Day03.md 文章中的特定概念，包含完整的程式碼範例和實際可執行的測試案例。

## 參考資源

- [Day 03 文章](../../Day03.md) - 完整的理論說明和概念介紹
- [xUnit 官方文件](https://xunit.net/docs/shared-context) - 進階資源管理
- [Test Data Builder 模式](http://www.natpryce.com/articles/000714.html) - Builder 模式詳細說明
