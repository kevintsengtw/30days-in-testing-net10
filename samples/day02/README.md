# Day 02 - xUnit 測試專案建立範例

這個專案對應 Day 02 文章「xUnit 框架深度解析」，示範如何從零建立一個 xUnit v3（Microsoft Testing Platform 模式）測試專案：類別庫加測試專案的標準結構、`[Fact]` 與 `[Theory]` 的基本用法。

## 專案結構

```
MyProject.sln
├── src/
│   └── MyProject.Core/               # 核心類別庫
│       └── Calculator.cs             # 計算器（Add、Subtract、Multiply、Divide、IsEven）
└── tests/
    └── MyProject.Core.Tests/         # 測試專案
        └── CalculatorTests.cs        # 計算器測試
```

## 開始使用

### 前置需求

- .NET 10 SDK
- Visual Studio 2022 或 VS Code

### 執行測試

```bash
# 進入專案目錄
cd samples/day02

# 建置專案
dotnet build MyProject.sln

# 執行所有測試
dotnet test --solution MyProject.sln

# 執行特定測試類別（xUnit v3 MTP 篩選語法）
dotnet test --solution MyProject.sln --filter-class "MyProject.Core.Tests.CalculatorTests"

# 執行測試並輸出 TRX 報告
dotnet test --solution MyProject.sln --report-trx --report-trx-filename day02.trx
```

## 技術規格

- **.NET**: 10.0
- **測試框架**: xunit.v3.mtp-v2 3.2.2（Microsoft Testing Platform 模式）
- **測試數量**: 32 個（5 個 Fact、5 個 Theory 展開 27 組資料，全數通過）

## 測試內容說明

`CalculatorTests` 針對 `Calculator` 的五個方法逐一驗證：

- **Add／Subtract／Multiply**：以 `[Fact]` 驗證基本情境，再用 `[Theory]` + `[InlineData]` 涵蓋各種數字組合（正數、負數、零、邊界值）
- **Divide**：驗證正常除法結果，並以 `Assert.Throws<DivideByZeroException>()` 驗證除數為零的例外
- **IsEven**：以 `[Theory]` 驗證奇偶判斷

測試命名採用 `被測試方法名稱_測試情境_預期行為` 格式，全部遵循 3A 模式（Arrange-Act-Assert）。

## 相關文章

請參考 [Day 02 - xUnit 框架深度解析 - 從生態概觀到實戰專案](../../Day02.md) 獲得完整的框架介紹與專案建立步驟。
