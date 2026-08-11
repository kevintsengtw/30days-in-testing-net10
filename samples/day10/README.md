# Day 10：AutoFixture 基礎：自動產生測試資料

## 🎯 專案概述

這個專案展示了 AutoFixture 的基礎使用方式，從 Day 03 的手動測試資料建立進化到 Day 10 的自動化產生。本專案包含了完整的業務邏輯實作和全面的測試覆蓋，特別針對循環參考等常見問題提供了實用的解決方案。

## 📁 專案結構

```text
Day10.AutoFixture/
├── Day10.AutoFixture.sln                    # 解決方案檔案
├── src/
│   └── AutoFixture.Core/                    # 核心業務邏輯
│       ├── Models/                          # 實體類別
│       │   ├── Customer.cs                 # 客戶實體（含循環參考）
│       │   ├── Order.cs                    # 訂單實體（含循環參考）
│       │   ├── Product.cs                  # 產品實體
│       │   ├── Address.cs                  # 地址實體
│       │   ├── Category.cs                 # 分類實體（循環參考）
│       │   └── Enums.cs                    # 列舉定義
│       ├── Services/                        # 服務類別
│       │   ├── OrderServices.cs           # 訂單相關服務
│       │   ├── UserServices.cs            # 使用者相關服務
│       │   └── BusinessServices.cs        # 商業邏輯服務
│       ├── Dtos/                           # 資料傳輸物件
│       │   └── RequestDtos.cs             # 請求 DTO
│       └── Validators/                      # 驗證器
│           └── Validators.cs              # 各種驗證器
└── tests/
    └── AutoFixture.Tests/                   # 測試專案
        ├── BasicGeneration/                 # 基本產生功能測試
        │   ├── BasicTypesGenerationTests.cs
        │   └── OmitAutoPropertiesTests.cs  # 屬性控制測試
        ├── ComplexObjects/                  # 複雜物件測試
        │   └── ComplexObjectCreationTests.cs
        ├── XunitIntegration/               # xUnit 整合測試
        │   ├── XunitIntegrationTests.cs
        │   └── SharedFixtureTests.cs
        ├── PracticalScenarios/             # 實務應用情境
        │   ├── EntityTests.cs
        │   ├── DtoValidationTests.cs
        │   └── LargeDataScenarioTests.cs
        ├── StabilityAndPredictability/     # 穩定性測試
        │   └── StabilityTests.cs
        ├── Comparison/                      # Day03 vs Day10 比較
        │   └── Day03VsDay10ComparisonTests.cs
        └── AdvancedPreview/                # 進階功能預覽
            └── AdvancedTechniquesPreviewTests.cs
```

## 核心概念展示

### 1. 基本型別自動產生

- **字串產生**：預設 GUID 格式，保證不重複
- **數值產生**：遞增序列，避免隨機性影響測試穩定性
- **日期時間**：合理的隨機日期時間值
- **特殊型別**：電子郵件、URI、版本號等格式正確的值

### 2. 複雜物件建構

- **巢狀物件**：自動建構多層物件結構
- **集合處理**：List、Array、Dictionary、HashSet 等集合型別
- **循環參考**：內建處理機制避免無限遞迴

### 3. xUnit 整合

- **測試方法層級**：每個測試獨立的 Fixture
- **類別層級共享**：共用客製化設定的 Fixture
- **Theory 整合**：與參數化測試的協作模式

### 4. 物件控制與客製化

- **OmitAutoProperties**：精確控制哪些屬性需要自動產生
- **With/Without**：手動設定或忽略特定屬性
- **Build 模式**：鏈式設定物件屬性

### 5. 實務應用情境

- **Entity 測試**：業務實體的邏輯驗證
- **DTO 驗證**：資料傳輸物件的驗證規則測試
- **大量資料模擬**：效能測試和批次處理測試

## 主要功能對比

| 功能                | 傳統手動方式      | AutoFixture      |
| ----------------- | ----------- | ---------------- |
| **資料準備**          | 40+ 行手動程式碼  | 5-10 行程式碼       |
| **維護成本**          | 物件改變需更新程式碼  | 自動適應           |
| **測試覆蓋**          | 有限的預定義案例    | 大量隨機組合         |
| **大量資料**          | 需要迴圈或複製     | `CreateMany(n)`  |
| **複雜物件結構**        | 手動建構每個層級    | 自動處理巢狀結構       |

## 快速開始

### 1. 建立 Fixture

```csharp
var fixture = new Fixture();
```

### 2. 基本用法

```csharp
// 基本型別
var name = fixture.Create<string>();
var age = fixture.Create<int>();

// 複雜物件
var customer = fixture.Create<Customer>();

// 集合
var customers = fixture.CreateMany<Customer>(10);
```

### 3. 客製化設定

```csharp
// 基本客製化
var order = fixture.Build<Order>()
    .With(x => x.Status, OrderStatus.Completed)
    .Without(x => x.Customer)
    .Create();

// 使用 OmitAutoProperties 精確控制
var customer = fixture.Build<Customer>()
    .OmitAutoProperties()           // 停用所有自動屬性
    .With(x => x.Id, 123)          // 只設定需要的屬性
    .With(x => x.Name, "測試客戶")
    .Create();
```

## 最佳實踐

### ✅ 建議做法

1. **專注於測試邏輯**：只設定測試真正關心的屬性值
2. **合理的生命週期管理**：根據需求選擇適當的 Fixture 範圍
3. **穩定性考量**：對關鍵業務邏輯設定固定值
4. **邊界值測試**：結合 AutoFixture 和明確的邊界值驗證

### ❌ 避免做法

1. **過度依賴隨機值**：不要假設隨機產生值的具體內容
2. **忽略循環參考**：注意處理可能的無限遞迴問題
3. **濫用自動產生**：簡單測試可能用固定值更清楚
4. **忽略效能影響**：大量資料產生時考慮效能

## 循環參考處理

```csharp
// 預設行為：拋出例外
var defaultFixture = new Fixture();

// 修改行為：忽略循環參考
fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
    .ForEach(b => fixture.Behaviors.Remove(b));
fixture.Behaviors.Add(new OmitOnRecursionBehavior());
```

## 進階功能預覽

Day 11 將介紹的進階功能：

- **客製化機制**：深度自訂物件產生策略
- **商業規則整合**：將業務邏輯融入測試資料產生
- **專業化 Customization**：可重用的客製化元件
- **AutoNSubstitute 整合**：自動產生 Mock 物件

## 執行測試

### 建置專案

```bash
dotnet build
```

### 執行所有測試

```bash
dotnet test
```

### 執行特定測試類別

```bash
dotnet test --filter-class "AutoFixture.Tests.BasicGeneration.BasicTypesGenerationTests"
```

## 套件依賴

- **AutoFixture** (4.18.1)：核心 AutoFixture 功能
- **AutoFixture.Xunit3** (4.19.0)：xUnit v3 整合
- **AwesomeAssertions** (9.5.0)：流暢的斷言語法
- **xunit.v3.mtp-v2** (3.2.2)：測試框架（Microsoft.Testing.Platform）

## 學習重點

1. **匿名測試概念**：專注於行為而非資料
2. **自動產生策略**：理解 AutoFixture 的產生邏輯
3. **物件圖建構**：掌握複雜結構的自動建立
4. **穩定性平衡**：在自動化和可預測性間取得平衡
5. **實務應用**：了解何時使用 AutoFixture，何時使用傳統方式

這個範例專案完整展示了 AutoFixture 的基礎功能，為進入 Day 11 的進階內容做好準備。
