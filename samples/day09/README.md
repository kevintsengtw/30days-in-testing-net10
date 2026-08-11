# Day 09：測試私有與內部成員 - Private 與 Internal 的測試策略

這個專案展示如何測試私有（private）和內部（internal）成員的各種技術與策略。

## 專案結構

```text
Day09.PrivateInternalTesting/
├── Day09.PrivateInternalTesting.sln
├── README.md
├── src/
│   └── Day09.Core/                    # 主要專案
│       ├── DataProcessor.cs           # 部分模擬範例
│       ├── PaymentProcessor.cs        # 包含私有方法的類別
│       ├── PriceCalculator.cs         # Internal 類別範例
│       ├── Models/                    # 付款相關資料模型
│       │   ├── PaymentMethod.cs
│       │   ├── PaymentRequest.cs
│       │   ├── PaymentResult.cs
│       │   ├── ProcessResult.cs
│       │   └── ValidationResult.cs
│       ├── StrategyPattern/           # 策略模式重構範例
│       │   ├── IDiscountStrategy.cs / StandardDiscountStrategy.cs
│       │   ├── ITaxStrategy.cs / TaiwanTaxStrategy.cs
│       │   ├── PricingService.cs
│       │   └── Customer.cs / Product.cs / Location.cs
│       └── GlobalUsings.cs
└── tests/
    └── Day09.Tests/                   # 測試專案
        ├── DataProcessorTests.cs      # 部分模擬測試（含 TestableDataProcessor）
        ├── PaymentProcessorTests.cs   # 私有方法測試
        ├── PriceCalculatorTests.cs    # Internal 成員測試
        ├── Helpers/
        │   └── ReflectionTestHelper.cs # 反射測試輔助類別
        ├── StrategyPattern/           # 策略模式測試
        │   ├── PricingServiceTests.cs
        │   ├── StandardDiscountStrategyTests.cs
        │   └── TaiwanTaxStrategyTests.cs
        └── GlobalUsings.cs
```

## 使用的套件版本

- .NET 10.0
- xunit.v3.mtp-v2 3.2.2（Microsoft Testing Platform 模式）
- NSubstitute 5.3.0
- AwesomeAssertions 9.5.0

## 核心概念展示

### 1. Internal 成員測試

**PriceCalculator.cs** - 展示如何使用 `InternalsVisibleTo` 測試內部類別：

```csharp
// 在 Day09.Core.csproj 中設定
<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
  <_Parameter1>Day09.Tests</_Parameter1>
</AssemblyAttribute>
```

### 2. 私有方法測試

**PaymentProcessor.cs** - 包含複雜私有邏輯的類別：

- `CalculateFee()` - 私有執行個體方法
- `IsBusinessDay()` - 私有靜態方法

**ReflectionTestHelper.cs** - 提供反射測試的輔助方法：

```csharp
// 測試私有執行個體方法
var result = ReflectionTestHelper.InvokePrivateMethod<decimal>(
    processor, "CalculateFee", amount, method);

// 測試私有靜態方法
var result = ReflectionTestHelper.InvokePrivateStaticMethod<bool>(
    typeof(PaymentProcessor), "IsBusinessDay", date);
```

### 3. 策略模式重構

**StrategyPattern.cs** - 展示如何將複雜私有邏輯重構為可測試的策略：

- `IDiscountStrategy` / `StandardDiscountStrategy` - 折扣計算策略
- `ITaxStrategy` / `TaiwanTaxStrategy` - 稅收計算策略  
- `PricingService` - 使用策略模式的定價服務

### 4. 部分模擬測試

**DataProcessor.cs** - 展示以繼承覆寫實作部分模擬，避免實際資料庫操作：

```csharp
/// <summary>
/// 可測試的 DataProcessor，覆寫 SaveData 避免實際資料庫操作
/// </summary>
public class TestableDataProcessor : DataProcessor
{
    protected override ProcessResult SaveData(string data)
    {
        // 模擬成功的儲存操作
        return ProcessResult.Success();
    }
}
```

> 這裡刻意不用 NSubstitute 的 `Substitute.ForPartsOf<T>()`——它無法直接設定 `protected` 成員的行為，
> 對 `protected virtual` 方法改用繼承覆寫反而簡單直接，也是實務上常見的技術選擇。

## 測試統計

共 34 個測試（含 Theory 展開）：

- Internal 成員測試（PriceCalculatorTests）：10 個
- 私有方法測試（PaymentProcessorTests）：13 個
- 策略模式測試（StrategyPattern/）：8 個
- 部分模擬測試（DataProcessorTests）：3 個

## 執行方式

### 建置專案

```powershell
dotnet build
```

### 執行測試

```powershell
dotnet test
```

### 執行特定測試類別

```powershell
dotnet test --filter-class "Day09.Tests.PriceCalculatorTests"
```

## 重點學習內容

### 測試策略決策原則

1. **設計優先**：好的設計自然有好的可測試性
2. **責任分離**：將複雜邏輯提取為獨立服務
3. **封裝平衡**：在封裝原則與測試需求間找到平衡

### Internal 成員測試技術

- **InternalsVisibleTo 屬性**：開放內部可見性給測試專案
- **適用情境**：框架開發、複雜內部演算法
- **風險評估**：維護成本 vs 測試價值

### 私有方法測試技術

- **反射呼叫**：使用 Reflection API 存取私有成員
- **輔助方法**：簡化反射操作的複雜性
- **使用時機**：複雜度高且難以透過公開方法測試

### 設計模式改善可測試性

- **策略模式**：將複雜私有邏輯重構為可替換元件
- **依賴注入**：讓原本難以測試的邏輯變得可測試
- **部分模擬**：混合真實與模擬行為的測試技術

### 實務決策框架

- **複雜度閾值**：超過 10 行且邏輯複雜
- **維護性優於覆蓋率**：避免脆弱的測試
- **業務價值導向**：專注於有價值的測試案例

## 注意事項

1. **避免過度測試私有方法**：通常表示設計問題
2. **重構優於直接測試**：策略模式、依賴注入等
3. **維護成本考量**：測試不應成為重構的阻礙
4. **封裝原則**：保持適當的訊息隱藏

這個專案展示了在實務中處理私有與內部成員測試的各種技術和權衡考量，幫助開發者在封裝性和可測試性之間找到適當的平衡。
