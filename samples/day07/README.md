# Day 07：依賴替代入門 - 使用 NSubstitute

這個範例專案展示了如何從不可測試的 Legacy Code 重構為可測試的設計，並使用 NSubstitute 進行單元測試。

## 專案結構

```
Day07.DependencyReplacement.sln
├── src/
│   ├── Day07.Legacy/                 # Legacy Code - 不可測試的版本
│   │   ├── FileBackupService.cs      # 直接依賴外部資源的服務
│   │   └── BackupResult.cs           # 回傳結果類別
│   └── Day07.Refactored/             # 重構後 - 可測試的版本
│       ├── Abstractions/             # 抽象介面
│       │   ├── IFileSystem.cs        # 檔案系統抽象
│       │   ├── IDateTimeProvider.cs  # 時間提供者抽象
│       │   └── IBackupRepository.cs  # 資料存取抽象
│       ├── Implementations/          # 具體實作
│       │   ├── FileSystemWrapper.cs  # 檔案系統實作
│       │   ├── DateTimeProvider.cs   # 時間提供者實作
│       │   └── SqlBackupRepository.cs # 資料存取實作
│       ├── FileBackupService.cs      # 重構後的服務
│       └── BackupResult.cs           # 回傳結果類別
└── tests/
    └── Day07.Tests/
        ├── FileBackupServiceTests.cs          # 單元測試
        └── FileBackupServiceCompositionTests.cs # 組裝 smoke test 範例
```

## 技術說明

### Legacy Code 的問題

`Day07.Legacy` 專案中的 `FileBackupService` 直接依賴：
- 檔案系統（`File.Exists`, `File.Copy`）
- 資料庫（`SqlConnection`）
- 時間（`DateTime.Now`）
- 控制台輸出（`Console.WriteLine`）

這些直接依賴導致測試困難：
- 需要實際檔案存在
- 需要資料庫連線
- 時間無法控制
- 無法驗證記錄行為

### 重構後的設計

`Day07.Refactored` 專案透過以下方式解決問題：

1. **依賴反轉原則**：依賴抽象介面而非具體實作
2. **介面隔離原則**：建立專注且小巧的介面
3. **單一職責原則**：每個類別只負責一個職責
4. **依賴注入**：透過建構函式注入依賴

### 使用的工具與技術

- **.NET 10**：目標框架 `net10.0`
- **xunit.v3.mtp-v2 3.2.2**：單元測試框架（Microsoft Testing Platform 模式）
- **NSubstitute 5.3.0**：測試替身框架
- **AwesomeAssertions 9.5.0**：斷言程式庫
- **Microsoft.Extensions.Logging 10.0.10**：結構化記錄
- **GlobalUsings.cs**：全域 using 語句管理

### 測試特色

1. **符合命名規範**：`被測試方法名稱_測試情境_預期行為`
2. **AwesomeAssertions 語法**：使用 `.Should().BeTrue()` 等語法
3. **完整的 ILogger 驗證**：使用底層 `Log` 方法驗證
4. **Test Double 類型展示**：Stub、Mock、Spy 的實際應用

## 執行測試

```bash
# 還原套件
dotnet restore Day07.DependencyReplacement.sln

# 建置專案（Release）
dotnet build Day07.DependencyReplacement.sln -c Release --no-restore

# 從 solution 執行所有測試並輸出 TRX
dotnet test --solution Day07.DependencyReplacement.sln -c Release --no-build \
    --report-trx --report-trx-filename day07.trx

# 只執行單一測試專案
dotnet test --project tests/Day07.Tests/Day07.Tests.csproj -c Release --no-build

# 產生程式碼覆蓋率報告（Microsoft Code Coverage extension）
dotnet test --solution Day07.DependencyReplacement.sln -c Release --no-build \
    --coverage --coverage-output-format cobertura --coverage-output day07.cobertura.xml
```

> xUnit v3 原生 MTP 模式不使用 VSTest 的 `--logger`、`--collect:"XPlat Code Coverage"` 參數；
> 覆蓋率改用 Microsoft Testing Platform 的 CodeCoverage extension 搭配 `--coverage` 系列選項。
> 也可以直接執行 `./run-tests.ps1`（PowerShell）或 `./run-tests.sh`（bash）一次跑完上述流程。

## 測試案例說明

### 單元測試（FileBackupServiceTests，6 個）

1. `BackupFileAsync_來源檔案存在且大小合理_應回傳成功結果`：正常備份流程
2. `BackupFileAsync_來源檔案不存在_應記錄警告並回傳失敗`：錯誤處理與記錄驗證
3. `BackupFileAsync_來源檔案存在且備份成功_應記錄資訊並回傳成功結果`：成功流程的完整依賴互動驗證
4. `BackupFileAsync_來源檔案大小超過限制_應記錄警告並回傳失敗`：業務規則與記錄驗證
5. `BackupFileAsync_資料庫儲存歷史記錄時拋出例外_應記錄錯誤並回傳失敗`：例外處理與記錄驗證
6. `BackupFileAsync_多次呼叫時_應產生唯一備份路徑`：時間戳功能驗證

### 組裝 smoke test（FileBackupServiceCompositionTests，1 個）

- `FileBackupService_以真實相依性組裝_應能成功建立實例`：示範如何組裝真實依賴，驗證 DI 接線

### Test Double 類型展示

- **Stub**：設定預期的回傳值（如檔案存在性、檔案大小）
- **Mock**：驗證方法呼叫行為（如記錄、資料庫操作）
- **Spy**：記錄呼叫資訊以便後續驗證

## 重要概念

### 1. 測試替身（Test Double）

- **Dummy**：填充參數，不會被使用
- **Stub**：回傳預設值，設定測試情境
- **Fake**：簡化的實作，如 In-Memory 資料庫
- **Spy**：記錄呼叫資訊
- **Mock**：驗證互動行為

### 2. NSubstitute 語法

```csharp
// 建立替身
var substitute = Substitute.For<IService>();

// 設定回傳值
substitute.Method().Returns(value);

// 驗證呼叫
substitute.Received(1).Method();

// 驗證未呼叫
substitute.DidNotReceive().Method();

// 拋出例外
substitute.Method().Throws(new Exception());

// 模擬 void／Task 方法拋出例外（本專案 SaveBackupHistoryAsync 的用法）
substitute.When(x => x.Method())
          .Do(x => throw new Exception());
```

### 3. 依賴注入的重要性

依賴注入是單元測試的基礎，它讓我們能夠：
- 替換外部依賴
- 控制測試環境
- 驗證互動行為
- 隔離測試邏輯

## 最佳實踐

1. **不要模擬值物件**：DateTime、string、int 等
2. **避免過度模擬**：只模擬需要控制的依賴
3. **區分狀態驗證與行為驗證**：選擇適當的驗證策略
4. **保持測試簡潔**：一個測試只驗證一個行為
5. **使用有意義的測試名稱**：清楚表達測試意圖

## 學習重點

透過這個範例，你將學會：
- 識別不可測試的程式碼
- 應用 SOLID 原則進行重構
- 使用 NSubstitute 建立測試替身
- 撰寫有效的單元測試
- 區分不同類型的測試替身

這是「重啟挑戰：老派軟體工程師的測試修練」第七天的完整範例，展示了從 Legacy Code 到可測試設計的完整轉換過程。
