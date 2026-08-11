# Day 11 - AutoFixture 進階：自訂化測試資料產生策略

## 專案概述

這個專案展示 AutoFixture 的進階使用技術，包括 DataAnnotations 整合、屬性值範圍控制、DateTime 範圍控制、數值範圍建構器與泛型數值範圍建構器等功能。

## 專案結構

```
Day11.AutoFixtureAdvanced.sln
├── src/
│   └── AutoFixtureAdvanced.Core/
│       ├── Person.cs                           # 包含 DataAnnotations 的人員類別
│       ├── Member.cs                           # 會員類別，用於展示範圍控制
│       ├── Product.cs                          # 產品類別，包含多種數值型別
│       └── Order.cs                            # 訂單類別，包含多種數值型別
└── tests/
    └── AutoFixtureAdvanced.Tests/
        ├── DataAnnotationsTests.cs             # DataAnnotations 整合測試
        ├── PropertyRangeTests.cs               # 屬性範圍控制測試
        ├── WithBehaviorTests.cs                # .With() 方法行為測試
        ├── DateTimeRangeTests.cs               # DateTime 範圍控制測試
        ├── RandomRangedNumericSequenceBuilderTests.cs # 數值範圍建構器測試
        ├── GenericNumericRangeBuilderTests.cs  # 泛型數值範圍建構器測試
        └── TestHelpers/
            ├── RandomRangedDateTimeBuilder.cs      # 自訂 DateTime 範圍建構器
            ├── RandomRangedNumericSequenceBuilder.cs # 第一版數值範圍建構器
            ├── ImprovedRandomRangedNumericSequenceBuilder.cs # 改進版數值範圍建構器
            ├── NumericRangeBuilder.cs               # 泛型數值範圍建構器
            └── FixtureRangedNumericExtensions.cs    # Fixture 擴充方法
```

## 使用的套件版本

- **.NET 10**
- **AutoFixture 4.18.1**
- **AwesomeAssertions 9.5.0**
- **xunit.v3.mtp-v2 3.2.2**（Microsoft.Testing.Platform）

## 主要功能展示

### 1. DataAnnotations 整合

- 自動識別 `StringLength`、`Range` 等屬性
- 產生符合驗證規則的測試資料

### 2. 屬性值範圍控制

- 使用 `.With()` 方法控制特定屬性值
- 固定值 vs 動態值的差異展示
- `Random.Shared` 的優勢說明

### 3. DateTime 範圍控制

- `RandomDateTimeSequenceGenerator` 的基本應用
- 自訂 `RandomRangedDateTimeBuilder` 解決特定屬性控制問題

### 4. 數值範圍建構器

- `RandomRangedNumericSequenceBuilder` 基礎實作（使用屬性名稱匹配）
- `ImprovedRandomRangedNumericSequenceBuilder` 改進版（使用條件函數）
- 展示 `Add()` 與 `Insert(0)` 在優先順序上的重要差異
- 解決 AutoFixture 內建數值產生器覆蓋自訂建構器的問題

### 5. 泛型數值範圍建構器

- `NumericRangeBuilder<TValue>` 支援所有數值型別（int、long、short、byte、float、double、decimal）
- `FixtureRangedNumericExtensions` 提供便利的擴充方法
- 支援複雜實體的多重數值型別範圍控制
- 完整的型別安全與泛型約束設計

## 執行方式

### 建置專案
```bash
dotnet build
```

### 執行測試
```bash
dotnet test
```

### 執行特定測試類別
```bash
# 測試 DataAnnotations 功能
dotnet test --filter-class "AutoFixtureAdvanced.Tests.DataAnnotationsTests"

# 測試屬性範圍控制
dotnet test --filter-class "AutoFixtureAdvanced.Tests.PropertyRangeTests"

# 測試 .With() 方法行為
dotnet test --filter-class "AutoFixtureAdvanced.Tests.WithBehaviorTests"

# 測試 DateTime 範圍控制
dotnet test --filter-class "AutoFixtureAdvanced.Tests.DateTimeRangeTests"
```

## 重點學習內容

### DataAnnotations 整合
- AutoFixture 能自動識別 DataAnnotation 屬性並產生符合限制的資料
- 支援 `StringLength`、`Range` 等常用驗證屬性

### .With() 方法的兩種用法
```csharp
// 固定值：只執行一次，所有物件使用同樣的值
.With(x => x.Age, Random.Shared.Next(30, 50))

// 動態值：每個物件都重新執行
.With(x => x.Age, () => Random.Shared.Next(30, 50))
```

### Random.Shared 的優勢
- 避免短時間內重複值問題
- 更好的效能表現
- 執行緒安全

### DateTime 範圍控制的進化
1. **RandomDateTimeSequenceGenerator**：影響所有 DateTime 屬性
2. **自訂 RandomRangedDateTimeBuilder**：可指定特定屬性

### 數值範圍建構器的演進
1. **RandomRangedNumericSequenceBuilder**：使用屬性名稱字串匹配
2. **ImprovedRandomRangedNumericSequenceBuilder**：使用條件函數判斷
3. **優先順序關鍵**：`Insert(0)` vs `Add()` 的重要差異

### 泛型數值範圍建構器的優勢
1. **型別安全**：`NumericRangeBuilder<TValue>` 提供編譯時期型別檢查
2. **支援完整數值型別**：int, long, short, byte, float, double, decimal
3. **擴充方法便利性**：`AddRandomRange<T, TValue>()` 提供流暢的設定介面
4. **統一轉換機制**：透過 decimal 作為中間型別進行安全轉換

## 技術要點

- 實作 `ISpecimenBuilder` 介面來自訂建構器
- 使用 `PropertyInfo` 識別目標屬性
- 回傳 `NoSpecimen` 表示無法處理該請求
- 使用 `HashSet<string>` 來管理目標屬性名稱
- **重要**：使用 `Customizations.Insert(0)` 確保自訂建構器優先權
- AutoFixture 內建建構器會覆蓋透過 `Add()` 新增的自訂建構器
- **泛型約束**：`where TValue : struct, IComparable, IConvertible` 確保數值型別限制
- **型別轉換策略**：統一使用 decimal 作為中間型別，避免精度損失

這個專案為 AutoFixture 的進階應用提供了完整的範例和最佳實務。
