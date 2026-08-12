# Day 16 - 測試日期與時間：Microsoft.Bcl.TimeProvider 取代 DateTime

## 專案描述

本專案展示如何使用 Microsoft.Bcl.TimeProvider 來解決單元測試中時間相依性的問題。透過時間抽象化，我們可以建立可控制、可重現的時間相依邏輯測試。

## 專案結構

```text
Day16.TimeTesting/
├── Day16.TimeTesting.sln
├── README.md
├── src/
│   └── Day16.TimeTesting.Core/        # 主要業務邏輯
│       ├── GlobalTimeService.cs       # 全球時間服務（時區轉換）
│       ├── OrderService.cs            # 訂單服務（營業時間、折扣邏輯）
│       ├── ScheduleService.cs         # 排程服務（工作調度）
│       ├── TimedCache.cs             # 定時快取（過期機制）
│       ├── TradingService.cs         # 交易服務（交易時間區間）
│       └── GlobalUsings.cs           # 全域 using 宣告
└── tests/
    └── Day16.TimeTesting.Tests/       # 測試專案
        ├── GlobalTimeServiceTests.cs  # 全球時間服務測試（時區處理）
        ├── OrderServiceTests.cs       # 訂單服務測試（傳統寫法）
        ├── OrderServiceAutoFixtureTests.cs    # 訂單服務測試（AutoFixture 寫法對照）
        ├── ScheduleServiceTests.cs    # 排程服務測試
        ├── TimedCacheTests.cs        # 定時快取測試
        ├── TradingServiceTests.cs    # 交易服務測試
        ├── FakeTimeProviderExtensions.cs      # FakeTimeProvider 擴充方法
        ├── AutoFixtureCustomizations.cs       # AutoFixture 自訂設定
        └── GlobalUsings.cs           # 全域 using 宣告
```

## 使用的套件版本

### 主要專案 (Day16.TimeTesting.Core)

- **Microsoft.Bcl.TimeProvider** 10.0.10 - 時間抽象層的核心套件（.NET 8+ 已內建 TimeProvider，此套件為舊框架的 polyfill）

### 測試專案 (Day16.TimeTesting.Tests)

- **Microsoft.Extensions.TimeProvider.Testing** 10.9.0 - FakeTimeProvider 測試工具
- **AwesomeAssertions** 9.5.0 - 斷言庫
- **AutoFixture** 4.18.1 / **AutoFixture.Xunit3** 4.19.0 / **AutoFixture.AutoNSubstitute** 4.18.1 - 自動化測試資料產生
- **NSubstitute** 6.2.0 - 模擬框架（全系列版本對齊）
- **xunit.v3.mtp-v2** 3.2.2 - 測試框架（Microsoft.Testing.Platform 模式）
- **Microsoft.Testing.Extensions.TrxReport** 2.3.3 - TRX 測試報告

> **關於 NU1608 警告**：NSubstitute 與全系列對齊升至 6.x，AutoFixture.AutoNSubstitute 4.18.1 宣告的相依上限是 NSubstitute < 6.0.0，因此 restore／build 會出現 NU1608 警告。這是預期行為，功能不受影響（70/70 測試通過），詳細說明見 Day13 文章「關於 NU1608 警告」一節。

## 重點學習內容

### 🔑 核心概念

1. **時間抽象化**：將時間取得邏輯抽象為可注入的服務
2. **測試可控性**：透過 FakeTimeProvider 完全控制測試中的時間
3. **並行安全**：每個測試都有獨立的時間環境

### 🛠️ 實戰技能

1. **基礎重構**：將 DateTime 相依程式碼改為使用 TimeProvider
2. **測試設計**：使用 FakeTimeProvider 進行時間控制測試
3. **AutoFixture 整合**：結合自動化測試資料產生與時間控制

### 進階應用

1. **時間控制技術**：凍結、快轉、倒轉等進階時間操作
2. **實戰情境**：排程系統、快取過期、交易時間區間等實際應用
3. **最佳實踐**：依賴注入、執行緒安全、測試隔離等重要考量

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
dotnet test --filter-class "Day16.TimeTesting.Tests.OrderServiceTests"
dotnet test --filter-class "Day16.TimeTesting.Tests.ScheduleServiceTests"
dotnet test --filter-class "Day16.TimeTesting.Tests.TimedCacheTests"
dotnet test --filter-class "Day16.TimeTesting.Tests.TradingServiceTests"
```

## 範例程式碼重點

### 1. OrderService - 營業時間與折扣邏輯

- 營業時間判斷：上午9點到下午5點
- 週五九折優惠
- 聖誕節八折優惠

### 2. ScheduleService - 排程系統

- 工作執行時間判斷
- Cron 表達式解析
- 下次執行時間計算

### 3. TimedCache - 定時快取

- 時間驅動的快取過期機制
- 自訂過期時間支援
- 多項目並行管理

### 4. TradingService - 交易時間區間

- 交易時間：9:00-11:30, 13:00-15:00
- 週末不交易
- 週五下午波動較大

### 5. 測試技術重點

- FakeTimeProvider 時間控制
- 時間快轉與凍結
- AutoFixture 整合
- 邊界條件測試

## 關鍵收穫

1. **可預測性**：時間相依的程式碼不再受執行環境影響
2. **可重現性**：測試結果完全可重現，消除隨機失敗
3. **完整性**：能夠測試所有時間相關的邊界條件和例外情況

透過 Microsoft.Bcl.TimeProvider，我們真正解決了時間測試的根本問題。不再需要擔心測試會因為執行時間而失敗，也不用為了測試特定時間點而等到半夜執行程式。

## 相關連結

- [TimeProvider 官方文件](https://learn.microsoft.com/zh-tw/dotnet/api/system.timeprovider)
- [Microsoft.Bcl.TimeProvider NuGet 套件](https://www.nuget.org/packages/Microsoft.Bcl.TimeProvider/)
- [Microsoft.Extensions.TimeProvider.Testing NuGet 套件](https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing/)
- [AwesomeAssertions GitHub](https://github.com/AwesomeAssertions/AwesomeAssertions)
