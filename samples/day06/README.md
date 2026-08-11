# Day 06 - Code Coverage 程式碼涵蓋範圍操作範例專案

## 專案說明

本專案是 Day 06「Code Coverage 程式碼涵蓋範圍實戰指南」的操作用範例：文章介紹的涵蓋率工具（Visual Studio 內建涵蓋率、MTP 原生涵蓋率擴充、ReportGenerator、VS Code 涵蓋率檢視）都可以拿這個專案實際操作。

程式碼內容沿用 Day 05 的 AwesomeAssertions 進階範例（改名為 `Day06.*`）——現成的 29 個測試與多層業務邏輯，正好是觀察涵蓋率的好素材；本專案另外加入了 `Microsoft.Testing.Extensions.CodeCoverage`，讓 MTP 模式下可直接輸出 cobertura 涵蓋率資料。

## 專案結構

```text
Day06.CodeCoverage.sln
├── src/
│   └── Day06.Domain/                    # 領域模型和服務（受測程式碼）
│       ├── Models/                      # 資料模型
│       │   ├── DomainModels.cs          # 主要業務模型
│       │   ├── RequestModels.cs         # 請求模型
│       │   └── Exceptions.cs            # 自訂例外
│       └── Services/                    # 業務服務
│           ├── BusinessServices.cs      # 商業邏輯服務
│           ├── UserServices.cs          # 使用者相關服務
│           └── ProcessingServices.cs    # 處理服務
└── tests/
    └── Day06.Tests/                     # 測試專案
        ├── AdvancedObjectGraphTests/     # 複雜物件比對測試
        ├── AdvancedAsyncAssertionTests/  # 進階非同步測試
        ├── AdvancedExceptionAssertionTests/ # 進階例外測試
        ├── CustomAssertionTests/         # 自訂 Assertions 測試
        ├── DynamicFieldExclusionTests/   # 動態欄位排除測試
        ├── PerformanceOptimizedTests/    # 效能最佳化與錯誤訊息測試
        └── Extensions/                   # 測試用擴展方法
            ├── CustomAssertions.cs       # 領域特定 Assertions
            └── PerformanceAssertions.cs  # 效能最佳化工具
```

## 開始使用

### 前置需求

- .NET 10 SDK
- Visual Studio 2026 或 VS Code

### 執行測試

```bash
# 進入專案目錄
cd samples/day06

# 建置專案
dotnet build Day06.CodeCoverage.sln

# 執行所有測試
dotnet test --solution Day06.CodeCoverage.sln

# 執行特定測試類別（xUnit v3 MTP 篩選語法）
dotnet test --solution Day06.CodeCoverage.sln --filter-class "Day06.Tests.AdvancedObjectGraphTests.AdvancedObjectGraphTests"
```

### 產生涵蓋率報告

```bash
# 執行測試並輸出 cobertura 涵蓋率資料（產生於 TestResults/coverage.cobertura.xml）
dotnet test --solution Day06.CodeCoverage.sln --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml

# 以 ReportGenerator 產生完整 HTML 報告（需先安裝：dotnet tool install -g dotnet-reportgenerator-globaltool）
reportgenerator -reports:TestResults/coverage.cobertura.xml -targetdir:coveragereport -reporttypes:Html
```

在 Visual Studio 2026 中則可直接使用內建涵蓋率：功能選單「`測試` → `分析所有測試的程式碼涵蓋範圍`」（自 VS2026 起 Community 與 Professional 版本皆可使用）。

## 技術規格

- **.NET**: 10.0
- **測試框架**: xunit.v3.mtp-v2 3.2.2（Microsoft Testing Platform 模式）
- **斷言庫**: AwesomeAssertions 9.5.0
- **涵蓋率**: Microsoft.Testing.Extensions.CodeCoverage 18.9.0（cobertura 輸出）
- **測試數量**: 29 個（全數通過）

## 相關文章

請參考 [Day 06 - Code Coverage 程式碼涵蓋範圍實戰指南](../../Day06.md) 獲得涵蓋率工具的完整介紹與判讀方法。
