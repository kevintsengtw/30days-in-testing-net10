# xUnit v2 → v3 專案升級 Checklist

這份清單可套用到一般專案；已勾選項目代表 Day26 範例的實作狀態。

## 1. 盤點

- [x] 確認 test project 是 SDK-style project。
- [x] 將 target framework 更新為 `net10.0`。
- [x] 記錄升級前測試結果：17 passed、0 failed。
- [x] 搜尋 `async void` 測試；Day26 找到 2 個。
- [x] 搜尋 `IAsyncLifetime`、`IDisposable` 及非同步清理邏輯。
- [x] 搜尋 `xunit.abstractions`、自訂 discoverer、runner reporter 與第三方 xUnit extension。
- [x] 決定使用 Microsoft Testing Platform，而不是保留 VSTest。

## 2. 套件與專案檔

- [x] 移除 v3 專案的 `xunit`。
- [x] 移除 v3 專案的 `Microsoft.NET.Test.Sdk`。
- [x] 移除 v3 專案的 `xunit.runner.visualstudio`。
- [x] 加入 `xunit.v3.mtp-v2` 3.2.2。
- [x] 加入 `Microsoft.Testing.Extensions.TrxReport` 2.3.2。
- [x] 將 `<OutputType>` 設為 `Exe`。
- [x] 保留 `<IsTestProject>true</IsTestProject>` 與 `<IsPackable>false</IsPackable>`。
- [x] 以 per-day `Directory.Packages.props` 固定直接套件版本。
- [x] 以 `global.json` 設定 .NET 10 SDK 與 `Microsoft.Testing.Platform` runner。

## 3. 程式碼相容性

- [x] 將 `async void` 測試改成 `async Task`。
- [x] `IAsyncLifetime.InitializeAsync` 改回傳 `ValueTask`。
- [x] `IAsyncLifetime.DisposeAsync` 改回傳 `ValueTask`。
- [x] 避免同一 fixture 同時以 sync 與 async disposal 重複清理。
- [x] v3 專案沒有 `xunit.abstractions` 相依。
- [x] 若仍使用 `ITestOutputHelper`，確認它來自 `Xunit` namespace；新程式碼也可使用 `TestContext.Current`。
- [x] 只使用官方存在的 v3 API，不宣稱 `[Test]` 或 retry attribute 是內建功能。

## 4. Runner 與 solution

- [x] 主 solution 只包含 MTP-compatible test project。
- [x] v2 對照專案不放入 MTP-only solution。
- [x] v2 專案以巢狀 `global.json` 選擇 VSTest，仍可獨立驗證。
- [x] CLI 使用 .NET 10 的 `dotnet test --solution ...` 格式。
- [x] TRX 由 `Microsoft.Testing.Extensions.TrxReport` 產生。

## 5. 驗證

- [x] v2：build 0 errors，只有 2 個預期的 `xUnit1048`。
- [x] v2：17 tests、17 passed、0 failed。
- [x] v3：restore 成功。
- [x] v3：build 0 errors、0 warnings。
- [x] v3：30 tests、28 passed、0 failed、2 skipped。
- [x] v3：連續執行第二次並產生 TRX。
- [x] v3 solution 的 `outdated`／`deprecated`／`vulnerable` package audit 全數通過。
- [x] 複製到 repo 外後 restore／build／test 通過。
- [ ] CI 使用的 .NET SDK、runner 與命令列已同步調整。

## 6. 專案實際使用時仍需確認

- [ ] 所有第三方 xUnit extension 都明確支援 v3。
- [ ] 自訂 test discoverer、test case 與 runner reporter 已依 v3 API 重寫或移除。
- [ ] CI、IDE 與測試報告工具能辨識所選的 MTP 版本。
- [ ] 若方案仍有 VSTest-only 專案，已拆分命令或完成 runner 遷移。
- [ ] 升級前後的測試數量、skip 與 filter 行為沒有非預期差異。
