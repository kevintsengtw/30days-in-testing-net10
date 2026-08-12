# Day 12 - 結合 AutoData：xUnit 與 AutoFixture 的整合應用

> **主題**：AutoData 屬性家族深度應用與外部資料整合  
> **學習重點**：AutoData、InlineAutoData、MemberAutoData、CompositeAutoData、CollectionSizeAttribute、資料來源設計模式

## 📚 專案概述

本專案展示了 xUnit 與 AutoFixture 的深度整合，透過 AutoData 屬性家族實現自動化測試資料產生，並結合外部資料來源（CSV/JSON）建立完整的測試資料管理策略。

## 🎯 學習目標

- ✅ 掌握 AutoData 屬性家族的核心概念與應用情境
- ✅ 實作 InlineAutoData 混合固定值與自動產生的技術
- ✅ 學習 MemberAutoData 整合外部資料來源的設計模式
- ✅ 建立 CollectionSizeAttribute 自訂屬性控制集合大小
- ✅ 解決 AutoFixture 循環參照問題的實務策略
- ✅ 設計階層式測試資料組織與重用機制
- ✅ 實現與 AwesomeAssertions 的無縫協作

## 🛠️ 技術堆疊

### 核心框架
- **.NET**: 10.0
- **C#**: 最新版本（top-level statements）
- **xunit.v3.mtp-v2**: 3.2.2（Microsoft.Testing.Platform）

### 測試相關套件
- **AutoFixture.Xunit3**: 4.19.0 - AutoData 屬性整合
- **AwesomeAssertions**: 9.5.0 - 現代化斷言語法
- **CsvHelper**: 33.1.0 - CSV 檔案讀取
- **System.Text.Json**: 內建 - JSON 資料處理

## 🏗️ 專案結構

```
Day12.AutoData/
├── Day12.AutoData.sln              # Solution 檔案
├── README.md                       # 專案說明文件
├── src/
│   └── AutoData.Core/              # 核心類別庫
│       ├── AutoData.Core.csproj
│       ├── Enums/
│       │   └── OrderStatus.cs     # 訂單狀態列舉
│       └── Models/                 # 資料模型
│           ├── CategorizedProduct.cs
│           ├── Customer.cs
│           ├── CustomerJsonRecord.cs
│           ├── Order.cs
│           ├── OrderItem.cs
│           ├── OrderResult.cs
│           ├── Person.cs
│           ├── Product.cs
│           └── ProductCsvRecord.cs
└── tests/
    └── AutoData.Tests/             # 測試專案
        ├── AutoData.Tests.csproj
        ├── GlobalUsings.cs         # 全域 using 宣告
        ├── Attributes/             # 自訂屬性
        │   ├── BusinessAutoDataAttribute.cs
        │   ├── CollectionSizeAttribute.cs
        │   ├── CompositeAutoDataAttribute.cs
        │   └── DomainAutoDataAttribute.cs
        ├── DataSources/            # 測試資料來源
        │   ├── BaseTestData.cs
        │   ├── CustomerTestDataSource.cs
        │   ├── ProductTestDataSource.cs
        │   └── ReusableTestDataSets.cs
        ├── TestData/               # 外部測試資料
        │   ├── customers.json
        │   └── products.csv
        └── 測試類別檔案/             # 各種測試範例
            ├── AutoDataBasicTests.cs
            ├── InlineAutoDataTests.cs
            ├── MemberAutoDataTests.cs
            ├── CompositeAutoDataTests.cs
            ├── CollectionSizeTests.cs
            ├── DataSourceDesignPatternTests.cs
            ├── AwesomeAssertionsCollaborationTests.cs
            └── ExternalDataIntegrationTests.cs
```

## 🚀 快速開始

### 1. 環境需求

```bash
# 確認 .NET 版本
dotnet --version  # 需要 9.0 或以上
```

### 2. 還原套件

```bash
# 在專案根目錄執行
dotnet restore
```

### 3. 編譯專案

```bash
# 編譯整個 solution
dotnet build

# 或者指定設定
dotnet build --configuration Release
```

### 4. 執行測試

```bash
# 執行所有測試
dotnet test

# 執行特定測試類別
dotnet test --filter-class "AutoData.Tests.AutoDataBasicTests"
```

## 📋 核心概念與範例

### AutoData 屬性家族

#### 1. AutoData - 完全自動產生
```csharp
[Theory]
[AutoData]
public void AutoData基本應用(Person person, Product product, int quantity)
{
    // 所有參數都由 AutoFixture 自動產生
    person.Should().NotBeNull();
    product.Should().NotBeNull();
    quantity.Should().BePositive();
}
```

#### 2. InlineAutoData - 混合固定值與自動產生
```csharp
[Theory]
[InlineAutoData("VIP", 100000)]
[InlineAutoData("Premium", 50000)]
public void InlineAutoData混合應用(string customerType, decimal creditLimit, Person person)
{
    // customerType 和 creditLimit 使用固定值
    // person 由 AutoFixture 自動產生
}
```

#### 3. MemberAutoData - 整合外部資料
```csharp
[Theory]
[MemberAutoData(nameof(BasicProducts))]
public void MemberAutoData外部資料整合(string name, decimal price, bool available, Customer customer)
{
    // name, price, available 來自 BasicProducts 方法
    // customer 由 AutoFixture 自動產生
}
```

### 自訂屬性實作

#### CollectionSizeAttribute - 控制集合大小
```csharp
[Theory]
[AutoData]
public void CollectionSize控制測試(
    [CollectionSize(5)] List<Product> products,
    [CollectionSize(3)] List<Order> orders)
{
    products.Should().HaveCount(5);
    orders.Should().HaveCount(3);
}
```

### 資料來源設計模式

#### 階層式資料組織
```csharp
// 基底類別
public abstract class BaseTestData
{
    protected static string GetTestDataPath(string fileName) => 
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", fileName);
}

// 產品資料來源
public class ProductTestDataSource : BaseTestData
{
    public static IEnumerable<object[]> BasicProducts() { ... }
    public static IEnumerable<object[]> ElectronicsFromCsv() { ... }
}
```

## 🧪 測試範例

### 測試統計
- **總測試數量**: 64 個
- **成功率**: 100%
- **涵蓋範例**: 8 個主要測試類別

> 遷移到 AutoFixture.Xunit3 後，`MemberAutoData` 會展開資料來源的每一列（Xunit2 版本有只跑第一列的 bug），因此測試案例數由 41 增加為 64。

### 主要測試類別

1. **AutoDataBasicTests** - AutoData 基礎應用
2. **InlineAutoDataTests** - 混合固定值與自動產生
3. **MemberAutoDataTests** - 外部資料整合
4. **CompositeAutoDataTests** - 複合屬性應用
5. **CollectionSizeTests** - 集合大小控制
6. **DataSourceDesignPatternTests** - 資料來源設計模式
7. **AwesomeAssertionsCollaborationTests** - 與 AwesomeAssertions 協作
8. **ExternalDataIntegrationTests** - 外部檔案整合

## 🔧 重要技術重點

### 1. InlineAutoData 限制
- 每個固定值都是 attribute argument，必須是**編譯期常數運算式**（`100 * 1000` 這種常數運算式可以，執行期方法呼叫不行）
- 型別也有限制：`decimal` 不是合法的 attribute 參數型別，需要 decimal 時改在 attribute 寫 int/double，由 xUnit 於執行期轉型
- 如需 decimal、變數或執行期計算的資料，使用 `MemberAutoData` 搭配靜態方法

### 2. MemberAutoData 代理方法
- 需要在測試類別中建立代理方法
- 解決命名空間參照問題

### 3. AutoFixture 循環參照
- 使用 `OmitOnRecursionBehavior` 處理循環參照
- 自訂 AutoData 屬性統一處理策略

### 4. 外部資料整合
- 支援 CSV 檔案讀取（CsvHelper）
- 支援 JSON 檔案讀取（System.Text.Json）
- 包含錯誤處理與預設資料機制

## 📊 效能與品質

### 編譯狀態
- ✅ **零錯誤、零警告**
- ✅ **所有套件相容**
- ✅ **符合 .NET 10 最佳實踐**

### 測試品質
- ✅ **100% 測試通過率**
- ✅ **完整的邊界測試**
- ✅ **例外情況處理**

## 🔍 學習資源

### 相關文章
- [Day 12 - 結合 AutoData：xUnit 與 AutoFixture 的整合應用](../../Day12.md)

### 參考文件
- [AutoFixture.xUnit3 Documentation](https://github.com/AutoFixture/AutoFixture.xUnit3)
- [AwesomeAssertions Documentation](https://github.com/AwesomeAssertions/AwesomeAssertions)
- [CsvHelper Documentation](https://joshclose.github.io/CsvHelper/)

## 🤝 相依專案

- [Day 10 - AutoFixture 基礎](../day10/) - AutoFixture 基礎概念
- [Day 11 - AutoFixture 進階](../day11/) - 自訂化測試資料產生

---

**📝 最後更新**: 2025-08-16<br>
**📊 測試狀態**: 64/64 通過 (100%)<br>
**🎯 學習重點**: AutoData 整合應用與外部資料管理
