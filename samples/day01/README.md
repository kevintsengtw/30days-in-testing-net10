# Day01 - FIRST 原則實作範例

這個專案展示了如何建立符合 FIRST 原則的單元測試，使用 .NET 10 和 xUnit v3（Microsoft Testing Platform 模式）。

## 專案結構

```
Day01.FirstPrinciples/
├── src/
│   └── Day01.Core/           # 核心類別庫
│       ├── Calculator.cs     # 基本計算器
│       ├── EmailHelper.cs    # Email 驗證工具
│       ├── OrderService.cs   # 訂單服務
│       ├── Counter.cs        # 計數器
│       └── PriceCalculator.cs # 價格計算器
└── tests/
    └── Day01.Core.Tests/     # 測試專案
        ├── CalculatorTests.cs      # 計算器測試
        ├── EmailHelperTests.cs     # Email 工具測試
        ├── CounterTests.cs         # 計數器測試
        ├── OrderServiceTests.cs    # 訂單服務測試
        └── PriceCalculatorTests.cs # 價格計算器測試
```

## FIRST 原則展示

### Fast (快速)
- 所有測試都不依賴外部資源
- 不連接資料庫、檔案系統或網路服務
- 每個測試都能在毫秒級完成

### Independent (獨立)
- 每個測試都能獨立執行
- 測試之間沒有相依關係
- 每個測試都建立自己的測試資料

### Repeatable (可重複)
- 每次執行都會得到相同結果
- 不依賴環境變數或外部狀態
- 使用固定的測試資料

### Self-Validating (自我驗證)
- 測試執行後能自動判斷成功或失敗
- 提供清楚的錯誤訊息
- 使用明確的斷言

### Timely (及時)
- 測試程式碼與產品程式碼同時開發
- 展示測試驅動開發的概念

## 使用的測試技術

### 基本測試
- `[Fact]` - 單一測試案例
- `[Theory]` + `[InlineData]` - 參數化測試
- 例外測試 - `Assert.Throws<T>()`

### 測試命名規範
採用 `被測試方法名稱_測試情境_預期行為` 的命名格式：
- `Add_輸入1和2_應回傳3()`
- `IsValidEmail_輸入null值_應回傳False()`
- `Calculate_輸入負數價格_應拋出ArgumentException()`

### 3A Pattern
所有測試都遵循 Arrange-Act-Assert 模式：
```csharp
[Fact]
public void Add_輸入1和2_應回傳3()
{
    // Arrange - 準備測試資料
    var calculator = new Calculator();
    var a = 1;
    var b = 2;
    var expected = 3;

    // Act - 執行被測試的方法
    var result = calculator.Add(a, b);

    // Assert - 驗證結果
    Assert.Equal(expected, result);
}
```

## 執行測試

```bash
# 建置專案
dotnet build Day01.FirstPrinciples.sln

# 從 solution 執行所有測試
dotnet test --solution Day01.FirstPrinciples.sln

# 執行特定測試類別（xUnit v3 MTP 篩選語法）
dotnet test --solution Day01.FirstPrinciples.sln --filter-class "Day01.Core.Tests.CalculatorTests"

# 執行測試並輸出 TRX 報告
dotnet test --solution Day01.FirstPrinciples.sln --report-trx --report-trx-filename day01.trx
```

## 技術規格

- **.NET**: 10.0
- **測試框架**: xunit.v3.mtp-v2 3.2.2（Microsoft Testing Platform 模式）
- **程式語言**: C#
- **IDE**: Visual Studio 2022 / VS Code
- **測試數量**: 82 個（全數通過，無外部相依，執行時間約 2 秒）

## 學習重點

1. **理解 FIRST 原則**：每個原則如何應用到實際測試中
2. **測試命名**：如何讓測試成為活的文件
3. **測試結構**：3A Pattern 的重要性
4. **例外測試**：如何正確測試例外情況
5. **參數化測試**：使用 Theory 減少重複程式碼

## 範例說明

### Calculator 類別（12 個測試）

展示基本的單元測試寫法，包括正常情況和除零例外處理。

### EmailHelper 類別（14 個測試）

展示如何測試驗證邏輯，使用 Theory 測試多種輸入案例，包含 null 值與各種無效格式。

### Counter 類別（10 個測試）

展示 Independent 和 Repeatable 原則，每個測試都建立獨立的實例。

### OrderService 類別（8 個測試）

展示如何測試業務邏輯，確保每次執行都得到一致的結果。

### PriceCalculator 類別（38 個測試）

展示 Self-Validating 原則，提供清楚的錯誤訊息和邊界值測試。

## 後續學習方向

- **Mock 物件**：使用 NSubstitute 隔離相依元件
- **進階斷言**：AwesomeAssertions 的流暢語法（本系列 Day04 起介紹）
- **測試資料產生**：AutoFixture 與 Bogus（本系列 Day10 起介紹）
- **測試組織**：Test Fixtures 與 Test Collections
- **測試覆蓋率**：程式碼覆蓋率分析

---

這個專案是「重啟挑戰：老派軟體工程師的測試修練」系列的第一天實作，展示了如何建立高品質的單元測試基礎。
