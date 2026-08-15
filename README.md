# 老派軟體工程師的測試修練 — 30 天挑戰（.NET 10 版）

「重啟挑戰：老派軟體工程師的測試修練」系列的 .NET 10 更新版。文章與範例專案全面升級到 .NET 10 與 xUnit v3（Microsoft Testing Platform 模式），每一篇技術文章的程式碼都對應一個可直接建置、執行的範例專案。

目前發佈 Day 01～30（Day 30 為系列總結）。另有一篇附錄 Day 31 介紹由本系列延伸發展的 AI 技能、流程與工具，待相關工具鏈定案後補上。

## 原版系列（2025 iThome 鐵人賽）

本系列改寫自 2025 iThome 鐵人賽「重啟挑戰：老派軟體工程師的測試修練」，原版以 .NET 9 撰寫：

- 系列文章：[重啟挑戰：老派軟體工程師的測試修練](https://ithelp.ithome.com.tw/users/20066083/ironman/8276)
- 原版範例程式：[kevintsengtw/30Days_in_Testing_Samples](https://github.com/kevintsengtw/30Days_in_Testing_Samples)

## 文章清單

### 一、先搞懂為什麼測，再談怎麼測（Day 01–06）

**[Day 01 - 老派工程師的測試啟蒙 - 為什麼我們需要測試？](Day01.md)** ｜ 範例：[samples/day01](samples/day01)

- 測試的本質：從「能跑就好」到「敢改」
- 測試金字塔：單元 70–80%、整合 15–25%、E2E 5–10%
- FIRST 原則：Fast、Independent、Repeatable、Self-Validating、Timely
- 3A Pattern 與命名規範 `被測試方法_測試情境_預期行為`
- AI 時代的測試挑戰：四種常見的失敗樣態

**[Day 02 - xUnit 框架深度解析 - 從生態概觀到實戰專案](Day02.md)** ｜ 範例：[samples/day02](samples/day02)

- xUnit、NUnit、MSTest 比較與選擇考量
- Fact vs Theory、InlineData／MemberData／ClassData
- 測試生命週期：建構式、IDisposable、IClassFixture
- 測試隔離深度解析：為什麼 xUnit 預設就隔離
- 建立第一個 xUnit v3 + MTP 測試專案

**[Day 03 - xUnit 進階功能與測試資料管理](Day03.md)** ｜ 範例：[samples/day03](samples/day03)

- Theory 進階資料提供：MemberData、ClassData、PropertyData
- Test Data Builder 模式與資料提供者模式
- IClassFixture 與 ICollectionFixture 的資源共享
- 並行執行機制與 `xunit.runner.json` 設定

**[Day 04 - AwesomeAssertions 基礎應用與實戰技巧](Day04.md)** ｜ 範例：[samples/day04](samples/day04)

- Fluent Assertions 商業授權變化與因應
- 物件、字串、數值、集合、例外的 Assertions
- 物件比對進階技巧
- 從傳統 Assert 升級的路徑

**[Day 05 - AwesomeAssertions 進階技巧與複雜情境應用](Day05.md)** ｜ 範例：[samples/day05](samples/day05)

- Object Graph 比對與循環參考處理
- 非同步與例外的進階 Assertions
- 自訂 Assertions 擴充
- 動態欄位排除與大量資料的效能策略

**[Day 06 - Code Coverage 程式碼涵蓋範圍實戰指南](Day06.md)** ｜ 範例：[samples/day06](samples/day06)

- 涵蓋率的價值與常見誤解
- MTP 原生涵蓋率擴充（`coverlet.collector` 已不適用）
- ReportGenerator 產生 HTML 報告與風險熱點
- 循環複雜度與測試案例數量的關係

### 二、讓程式碼可以被測（Day 07–09）

**[Day 07 - 相依替代入門 - 使用 NSubstitute](Day07.md)** ｜ 範例：[samples/day07](samples/day07)

- 為什麼需要測試替身：隔離，不是模擬
- SOLID 原則對單元測試的影響
- Test Double 五型：Dummy、Stub、Fake、Spy、Mock
- NSubstitute 完整語法與 ILogger 特殊處理
- Moq 的 SponsorLink 爭議與選型考量

**[Day 08 - 測試輸出與記錄 - xUnit ITestOutputHelper 與 ILogger](Day08.md)** ｜ 範例：[samples/day08](samples/day08)

- ITestOutputHelper 的正確注入與生命週期
- ILogger 擴充方法的攔截技巧
- XUnitLogger 與 CompositeLogger 的組合應用
- xUnit1051：測試裡的 CancellationToken

**[Day 09 - 測試私有與內部成員 - Private 與 Internal 的測試策略](Day09.md)** ｜ 範例：[samples/day09](samples/day09)

- 封裝原則與測試需求的平衡
- Internal 成員的三種可見性設定方式
- 反射測試私有方法與其風險
- 用策略模式改善可測試性
- 決策樹：什麼時候該測、什麼時候該改設計

### 三、測試資料不用再手刻（Day 10–15）

**[Day 10 - AutoFixture 基礎：自動產生測試資料](Day10.md)** ｜ 範例：[samples/day10](samples/day10)

- 匿名測試的概念與價值
- 複雜物件建構與循環參考處理
- 與 xUnit 的整合應用
- 與 Day 03 Test Data Builder 的分工

**[Day 11 - AutoFixture 進階：自訂化測試資料產生策略](Day11.md)** ｜ 範例：[samples/day11](samples/day11)

- DataAnnotations 整合
- `.With()` 與 `Random.Shared` 的正確用法
- 自訂 `ISpecimenBuilder` 與 `NoSpecimen` 的回傳時機
- `Customizations.Insert(0)` 與建構器優先順序
- 泛型化的數值範圍建構器

**[Day 12 - 結合 AutoData：xUnit 與 AutoFixture 的整合應用](Day12.md)** ｜ 範例：[samples/day12](samples/day12)

- AutoData 屬性家族：AutoData、InlineAutoData、MemberAutoData
- attribute argument 的編譯期常數限制
- CSV／JSON 外部測試資料整合
- CompositeAutoData 與 CollectionSize

**[Day 13 - NSubstitute 與 AutoFixture 的整合應用](Day13.md)** ｜ 範例：[samples/day13](samples/day13)

- AutoNSubstituteCustomization 自動建立測試替身
- `[Frozen]` 的作用與使用時機
- 自訂 AutoData 屬性（含 Xunit3 的簽章變更）
- 什麼時候該退回較明確的測試設定

**[Day 14 - Bogus 入門：與 AutoFixture 的差異比較](Day14.md)** ｜ 範例：[samples/day14](samples/day14)

- `Faker<T>` 與 `RuleFor` 基本語法
- 內建 DataSet 與多語言支援
- Bogus vs AutoFixture 的取捨
- AutoBogus 已停維，改用 `Faker<T>` 的做法

**[Day 15 - AutoFixture 與 Bogus 的整合應用](Day15.md)** ｜ 範例：[samples/day15](samples/day15)

- 用 `ISpecimenBuilder` 讓 Bogus 接管特定欄位
- 循環參考的兩種處理行為
- Seed 可重現性的實際限制

### 四、把難測的東西變可測（Day 16–18）

**[Day 16 - 測試日期與時間：Microsoft.Bcl.TimeProvider 取代 DateTime](Day16.md)** ｜ 範例：[samples/day16](samples/day16)

- 時間相依的三個測試困境
- TimeProvider 架構與 FakeTimeProvider
- 時間快轉、歷史重播、時區處理
- 什麼不該測：效能斷言與記憶體斷言的陷阱

**[Day 17 - 檔案與 IO 測試：使用 System.IO.Abstractions 模擬檔案系統 - 打造可測試的檔案操作](Day17.md)** ｜ 範例：[samples/day17](samples/day17)

- 檔案系統測試的四個根本挑戰
- `IFileSystem` 介面與 MockFileSystem
- 目錄操作、串流與大檔案測試
- MockFileSystem 的邊界：例外情境要配 NSubstitute

**[Day 18 - 驗證測試：FluentValidation Test Extensions](Day18.md)** ｜ 範例：[samples/day18](samples/day18)

- FluentValidation vs DataAnnotation
- Test Extensions 的基本與進階用法
- 跨欄位、條件式與非同步驗證
- 年齡驗證的時間處理：`GetLocalNow()` 的業務語意

### 五、從單元走到整合（Day 19–25）

**[Day 19 - 整合測試入門：基礎架構與應用情境](Day19.md)** ｜ 範例：[samples/day19](samples/day19)

- 整合測試的定義、價值與成本效益
- `WebApplicationFactory<T>` 與 TestServer
- AwesomeAssertions.Web 的 `Satisfy<T>`
- 故障排除：套件相容性與 `PostAsJsonAsync` 撞名（CS0121）
- 三個學習層級：從簡單 WebApi 到完整專案

**[Day 20 - Testcontainers 初探：使用 Docker 架設測試環境](Day20.md)** ｜ 範例：[samples/day20](samples/day20)

- EF Core InMemory 的限制與原子性操作
- 容器生命週期與 Wait Strategy
- PostgreSQL、SQL Server、Redis、WireMock 實作
- xUnit v3 的 `IAsyncLifetime` 改用 `ValueTask`

**[Day 21 - Testcontainers 整合測試：MSSQL + EF Core 以及 Dapper 基礎應用](Day21.md)** ｜ 範例：[samples/day21](samples/day21)

- Collection Fixture 解決容器啟動的效能瓶頸
- Repository Pattern 與介面分離原則
- SQL 指令碼外部化策略
- EF Core 與 Dapper 的測試重點差異

**[Day 22 - Testcontainers 整合測試：MongoDB 及 Redis 基礎到進階](Day22.md)** ｜ 範例：[samples/day22](samples/day22)

- MongoDB 文件模型、BSON 序列化與索引
- Redis 五種資料結構的測試
- 資料隔離與清理策略
- 容器共用可以共享基礎設施，但不能共享資料

**[Day 23 - 整合測試實戰：WebApi 服務的整合測試](Day23.md)** ｜ 範例：[samples/day23](samples/day23)

- Clean Architecture 專案的整合測試設計
- `IExceptionHandler` 與 ProblemDetails（RFC 9457）
- 反向驗證：怎麼證明測試真的穿越了 handler
- Respawn 資料清理與 TimeProvider 注入

**[Day 24 - .NET Aspire Testing 入門基礎介紹](Day24.md)** ｜ 範例：[samples/day24](samples/day24)

- `Aspire.Hosting.Testing` 與 AppHost 重用
- Resource readiness：不要用 `Running` 加固定延遲
- schema 初始化的競態與資料隔離
- 交易測試不要捕捉自己的 assertion failure

**[Day 25 - .NET Aspire 整合測試實戰：從 Testcontainers 到 .NET Aspire Testing](Day25.md)** ｜ 範例：[samples/day25](samples/day25)

- 遷移前先保留 baseline 的價值
- AppHost 編排 PostgreSQL、Redis 與 Web API
- Aspire 13.4 的 HTTPS-first 行為變更
- Testcontainers 還是 Aspire Testing：選型判準

### 六、框架會變，遷移是常態（Day 26–29）

**[Day 26 - 從 xUnit v2 升級到 xUnit v3：.NET 10 與 Microsoft Testing Platform 遷移指南](Day26.md)** ｜ 範例：[samples/day26](samples/day26)

- 保留 v2 baseline 作為對照
- 三個 breaking change：`async void`、`IAsyncLifetime`、`xunit.abstractions`
- v3 新功能：TestContext、dynamic skip、MatrixTheoryData、assembly fixture
- 建議的實際升級順序

**[Day 27 - TUnit 入門：在 .NET 10 使用 Microsoft Testing Platform](Day27.md)** ｜ 範例：[samples/day27](samples/day27)

- Source Generator 測試發現與 MTP 執行平台
- 預設並行執行與 `[NotInParallel]` 的定位
- `[Test]`、`[Arguments]` 與 awaitable assertions
- Native AOT 要不要現在就用

**[Day 28 - TUnit 進階：資料來源、生命週期與 Dependency Injection](Day28.md)** ｜ 範例：[samples/day28](samples/day28)

- `MethodDataSource`、`ClassDataSource`、Matrix Tests 的選用
- 測試生命週期 hooks 的四種範圍
- TUnit 的原生 DI 支援與適用時機
- `[Property]` 與 tree node filter

**[Day 29 - TUnit 實戰：執行控制、ASP.NET Core 與 Testcontainers](Day29.md)** ｜ 範例：[samples/day29](samples/day29)

- `[Retry]` 只用於可辨識的暫時性錯誤
- `[Timeout]` 與 cancellation token 的傳遞
- `TUnit.AspNetCore` 與 `TestWebApplicationFactory<T>`
- Assembly hooks 管理 PostgreSQL、Redis、Kafka

### 總結（Day 30）

**[Day 30 - 重啟挑戰的測試修練總結：從基礎到實戰的 30 天回顧與 AI 時代的開發與測試模式轉變的想法](Day30.md)**

- 第一章逐日回顧：29 天各有內容大綱、文章連結與範例連結，六個篇章各附學習心得回顧
- 第二章個人實務心得：團隊導入、涵蓋率迷思、TDD 立場、並行執行、工具選型
- 第三章 AI 時代的開發與測試模式轉變
- 第四章持續精進與未來方向

## 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 任一開發工具：Visual Studio 2022 以上、JetBrains Rider、VS Code
- Docker（Day 19～25 與 Day 29 的整合測試範例需要：Testcontainers 與 .NET Aspire）

## 執行範例

每天的範例都內含自己的 `Directory.Packages.props`（CPM）與 `global.json`，可以獨立建置，不相依 repo 其他部分：

```bash
cd samples/day01
dotnet test --solution Day01.FirstPrinciples.sln
```

各天的執行方式與測試說明見該目錄的 README。

## 測試框架說明

範例使用 `xunit.v3.mtp-v2`（xUnit v3 的 Microsoft Testing Platform 模式），並採雙軌設定：

- **命令列**：`dotnet test` 依 `global.json` 的設定走 MTP
- **IDE 測試總管**：Visual Studio 與 Rider 靠 `Microsoft.NET.Test.Sdk` 與 `xunit.runner.visualstudio` 走 VSTest 探索

這是 xUnit 官方建議的過渡期做法——IDE 對 MTP 的支援還在跟進中，兩套並存可以讓命令列與測試總管都正常運作。

## 套件版本

各天套件版本集中在該天的 `Directory.Packages.props` 管理，csproj 內不寫版本號。文章開頭的 front-matter 宣告該篇相依的套件與範例專案。
