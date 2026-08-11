# Day 05 - AwesomeAssertions 進階技巧範例專案

## 專案說明

本專案展示了 AwesomeAssertions 的進階應用技巧，包括：

- 複雜物件比對技巧
- 自訂 Assertions 擴展
- 動態欄位排除處理
- 效能最佳化 Assertions
- 非同步 Assertions 進階技巧

## 專案結構

```
Day05.AwesomeAssertionsAdvanced.sln
├── src/
│   └── Day05.Domain/                    # 領域模型和服務
│       ├── Models/                      # 資料模型
│       │   ├── DomainModels.cs          # 主要業務模型
│       │   ├── RequestModels.cs         # 請求模型
│       │   └── Exceptions.cs            # 自訂例外
│       └── Services/                    # 業務服務
│           ├── BusinessServices.cs      # 商業邏輯服務
│           ├── UserServices.cs          # 使用者相關服務
│           └── ProcessingServices.cs    # 處理服務
└── tests/
    └── Day05.Tests/                     # 測試專案
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
- Visual Studio 2022 或 VS Code

### 執行測試

```bash
# 進入專案目錄
cd samples/day05

# 建置專案
dotnet build Day05.AwesomeAssertionsAdvanced.sln

# 執行所有測試
dotnet test --solution Day05.AwesomeAssertionsAdvanced.sln

# 執行特定測試類別（xUnit v3 MTP 篩選語法）
dotnet test --solution Day05.AwesomeAssertionsAdvanced.sln --filter-class "Day05.Tests.AdvancedObjectGraphTests.AdvancedObjectGraphTests"

# 執行測試並輸出 TRX 報告
dotnet test --solution Day05.AwesomeAssertionsAdvanced.sln --report-trx --report-trx-filename day05.trx
```

## 技術規格

- **.NET**: 10.0
- **測試框架**: xunit.v3.mtp-v2 3.2.2（Microsoft Testing Platform 模式）
- **斷言庫**: AwesomeAssertions 9.5.0
- **測試數量**: 29 個（全數通過）

## 重點特色

1. **複雜物件比對**：循環參考處理、Object Graph 深度比較、大型物件圖的比較策略
2. **進階非同步 Assertions**：執行時間驗證、CancellationToken 處理、並行任務驗證
3. **自訂 Assertions 擴展**：電商領域特定 Assertions（`BeValidProduct`、`BeValidOrder`）、條件式 Assertions 建構器
4. **動態欄位排除**：時間戳記自動排除、巢狀物件欄位排除、命名慣例排除
5. **效能最佳化**：大量資料分批處理、關鍵屬性快速比對、抽樣驗證策略
6. **錯誤訊息最佳化**：詳細的錯誤上下文、可操作的失敗資訊

## 相關文章

請參考 [Day 05 - AwesomeAssertions 進階技巧與複雜情境應用](../../Day05.md) 獲得完整的理論基礎與最佳實踐指南。
