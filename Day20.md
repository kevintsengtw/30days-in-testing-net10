---
day: 20
title: "Day 20 - Testcontainers 初探：使用 Docker 架設測試環境"
sample: samples/day20
target_framework: net10.0
packages:
  - AwesomeAssertions
  - Microsoft.EntityFrameworkCore
  - Microsoft.EntityFrameworkCore.SqlServer
  - Microsoft.Testing.Extensions.TrxReport
  - Npgsql.EntityFrameworkCore.PostgreSQL
  - StackExchange.Redis
  - SSH.NET
  - Testcontainers
  - Testcontainers.MsSql
  - Testcontainers.PostgreSql
  - Testcontainers.Redis
  - WireMock.Net.Testcontainers
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 20 - Testcontainers 初探：使用 Docker 架設測試環境

<!-- toc -->

- [前言](#前言)
- [本篇內容](#本篇內容)
- [Testcontainers 框架介紹](#testcontainers-框架介紹)
- [環境準備](#環境準備)
- [基本容器操作與 Wait Strategy](#基本容器操作與-wait-strategy)
- [資料庫整合測試](#資料庫整合測試)
- [外部服務模擬測試](#外部服務模擬測試)
- [總結](#總結)
- [在本機執行測試（MTP）](#在本機執行測試mtp)
- [參考資料](#參考資料)

<!-- /toc -->

## 前言

在 ASP.NET Core 中，使用 Entity Framework Core (EF Core) 的 InMemory 資料庫來進行單元測試是一種常見且有效的方法，因為它速度快且不需要實際的資料庫連線。

InMemory 模式無法模擬真實資料庫的所有行為。若測試牽涉 SQL 語意、交易或並行控制，就得先看清楚它的限制。

以下比較 EF Core InMemory 與真實資料庫（例如 SQL Server、PostgreSQL）的主要差異：

1. **交易行為與資料庫鎖定**
   - InMemory 不支援資料庫交易（Transactions），這意味著 SaveChanges() 成功後資料會立即儲存，不會像在真實資料庫中那樣，可以將多個操作包裝在一個原子性的交易中，並在發生錯誤時進行 Rollback。因此，涉及複雜交易邏輯的測試無法在 InMemory 模式下進行。
   - InMemory 沒有資料庫鎖定（Locking）機制，無法模擬多人同時修改同一筆資料的並行（Concurrency）行為。

2. **LINQ 查詢的差異**
   - InMemory 資料庫會直接在記憶體中執行 LINQ 查詢，這與真實資料庫透過 SQL 語法進行查詢有本質上的不同。
   - 查詢翻譯差異：某些 LINQ 查詢，例如複雜的 GroupBy、JOIN、OrderBy 或自訂函式，在 InMemory 中可能可以正常執行，但在轉換成 SQL 時可能會失敗或產生不同的結果。
   - Case Sensitivity：真實資料庫的字串比較行為取決於其校對規則（Collation），有些是區分大小寫的（Case-sensitive），有些則否。而 InMemory 的行為預設為不區分大小寫，這可能導致測試結果與實際執行時的行為不一致。
   - 效能模擬不足：InMemory 測試無法模擬真實資料庫在執行複雜查詢時可能遇到的效能瓶頸或索引（Index）問題。

3. **資料庫特定行為與功能**
   - InMemory 模式無法測試以下依賴於真實資料庫的功能：
     - 預存程序 (Stored Procedures) 與 Triggers：這些是資料庫伺服器上的程式碼，InMemory 無法模擬。
     - Views：類似於預存程序，Views 是資料庫物件，無法在 InMemory 中建立或查詢。
     - 資料庫約束 (Constraints)：如外來鍵約束 (Foreign Key Constraints)、檢查約束 (Check Constraints) 或唯一約束 (Unique Constraints)，在 InMemory 中雖然可以進行一些基本的檢查，但其行為與真實資料庫的嚴格性仍有差異。例如，刪除父資料時，外來鍵的 Cascade Delete 行為無法完全模擬。
     - 資料類型與精確度：不同的資料庫對於資料類型（如 decimal 的精確度、datetime 的範圍等）有不同的實現，InMemory 傾向於使用 .NET 的標準類型，這可能無法捕捉到真實資料庫中潛在的資料精確度或溢位問題。
     - Concurrency Tokens：如 RowVersion 或 Timestamp，這些是資料庫提供的用於解決並行衝突的機制，InMemory 無法準確模擬其自動更新的行為。

**InMemory 資料庫 - 小結**
InMemory 資料庫是一個非常適合進行單元測試的工具，特別是在測試 Repository 模式或 Service 層的 CRUD (建立、讀取、更新、刪除) 邏輯時。它可以快速驗證商業邏輯是否正確，而無需依賴外部服務。

然而，如果你的測試需要驗證：

- 涉及複雜交易的商業邏輯
- 並行情境下的資料處理
- 特定於資料庫的效能、查詢翻譯或行為
- 與預存程序、Triggers 等資料庫物件的互動

那麼，你應該考慮使用整合測試，並連接到一個輕量級的真實資料庫（不要用 SQLite，因為也是無法測試到實際複雜的行為）。這樣才測得到應用程式在正式環境的真實行為。

### 什麼是原子性操作？

原子性操作（Atomic Operation）是指一個或一系列的程式碼操作，在執行時要麼全部成功完成，要麼全部不執行，不存在部分完成的狀態。這就好比一個原子是不可再分的，原子性操作也是不可再分的。

---

這些限制正是引入 **Testcontainers** 的理由。測試會啟動真正的 Docker 容器，因此可以驗證交易、並行控制與資料庫特有功能，行為也更接近正式環境。

## 本篇內容

今天的內容有：

- **認識 Testcontainers 的概念與優勢**：了解容器化測試如何解決傳統測試的限制
- **掌握基本容器操作與生命週期管理**：容器的建立、設定與管理策略
- **實作基礎資料庫容器測試**：PostgreSQL 和 SQL Server 的整合測試操作
- **學習外部服務的基礎模擬**：使用 WireMock 容器模擬 HTTP API 服務與 Redis 快取服務測試

## Testcontainers 框架介紹

### 什麼是 Testcontainers？

Testcontainers 是一個測試函式庫，提供輕量好用的 API 來啟動 Docker 容器，專門用於整合測試。簡單說就是「在測試程式碼中管理 Docker 容器」。

這個概念解決了一個長期困擾開發者的問題：如何在測試中使用真實的外部服務（如資料庫、訊息佇列），而不需要在每台開發機器上手動安裝和設定這些服務。

核心概念很簡單：

```csharp
// 建立 PostgreSQL 容器
var postgres = new PostgreSqlBuilder("postgres:15-alpine")
    .WithDatabase("testdb")
    .WithUsername("test")
    .WithPassword("test")
    .Build();

// 啟動容器
await postgres.StartAsync();

// 使用容器的連線字串進行測試
var connectionString = postgres.GetConnectionString();

// 測試完成後自動清理容器
await postgres.DisposeAsync();
```

### 容器化測試的優勢

與傳統的 Mock 物件相比，Testcontainers 提供了以下優勢：

#### 1. **真實環境測試**

使用真實的資料庫、訊息佇列等服務，可以驗證實際 SQL 語法、資料庫限制條件與資料存取層行為。這些正是模擬物件無法涵蓋的部分。

#### 2. **環境一致性**

確保測試環境與正式環境使用相同的服務版本。避免因為版本差異導致的測試結果不準確，讓測試更具可信度。

#### 3. **清潔的測試環境**

每個測試都有獨立、乾淨的環境，避免測試間的干擾。容器會在測試結束後自動清理，確保下次測試不會受到前一次測試資料的影響。

#### 4. **簡化開發環境設定**

開發者不需要在本機安裝各種服務，只需要有 Docker。這大幅降低了新人加入專案的門檻，也避免了因為本機環境差異而導致的測試結果不一致問題。

### 與傳統 Mock 的差異

先比較 Mock 與 Testcontainers：

**使用 NSubstitute Mock**：

```csharp
[Fact]
public async Task GetUserAsync_輸入使用者ID1_應回傳對應使用者()
{
    // Arrange
    var mockRepository = Substitute.For<IUserRepository>();
    mockRepository.GetByIdAsync(1).Returns(new User { Id = 1, Name = "Test" });
    
    var userService = new UserService(mockRepository);
    
    // Act
    var user = await userService.GetUserAsync(1);
    
    // Assert
    user.Should().NotBeNull();
    user.Name.Should().Be("Test");
}
```

**使用 Testcontainers**：

```csharp
[Fact]
public async Task GetUserAsync_使用真實資料庫_應回傳正確使用者資料()
{
    // Arrange
    await using var postgres = new PostgreSqlBuilder("postgres:15-alpine").Build();
    await postgres.StartAsync();
    
    var connectionString = postgres.GetConnectionString();
    var repository = new UserRepository(connectionString);
    var userService = new UserService(repository);
    
    // 先建立測試資料
    await repository.CreateUserAsync(new User { Id = 1, Name = "Test" });
    
    // Act
    var user = await userService.GetUserAsync(1);
    
    // Assert
    user.Should().NotBeNull();
    user.Name.Should().Be("Test");
}
```

Mock 測試速度快但只測試邏輯，Testcontainers 測試較慢但能測試完整的資料存取流程。

**Mock 測試的特點**：

- 執行速度快，通常幾毫秒就完成
- 專注於業務邏輯的測試
- 不會發現資料存取層的問題
- 適合單元測試

**Testcontainers 測試的特點**：

- 執行時間較長，需要啟動容器
- 能測試完整的資料流程
- 可以發現 SQL 語法錯誤、資料庫限制等問題
- 適合整合測試

兩種方法各有優勢，在實際專案中通常會混合使用。

### .NET Testcontainers 生態系

> 以下的 `4.13.0` 是本系列鎖定的版本，不代表你閱讀時的最新版；套件會持續更新，實際版本以各日 sample 的 `Directory.Packages.props` 為準。

.NET 的 Testcontainers 提供完整的生態系：

#### 核心套件

```xml
<!-- 基礎 Testcontainers 功能 -->
<PackageReference Include="Testcontainers" Version="4.13.0" />
```

#### 專用模組套件

```xml
<!-- 資料庫 -->
<PackageReference Include="Testcontainers.PostgreSql" Version="4.13.0" />
<PackageReference Include="Testcontainers.MsSql" Version="4.13.0" />
<PackageReference Include="Testcontainers.MongoDb" Version="4.13.0" />

<!-- 快取與訊息佇列 -->
<PackageReference Include="Testcontainers.Redis" Version="4.13.0" />
<PackageReference Include="Testcontainers.RabbitMq" Version="4.13.0" />
```

### 支援的容器類型概覽

Testcontainers for .NET 支援廣泛的容器類型，幾乎涵蓋了現代應用程式需要的所有外部服務：

- **關聯式資料庫**：PostgreSQL、SQL Server、MySQL、Oracle
  用於測試資料存取層、Entity Framework Core 整合等

- **NoSQL 資料庫**：MongoDB、Cassandra、CouchDB  
  適合測試文件儲存、大資料應用的資料存取

- **快取服務**：Redis、Memcached  
  測試快取策略、分散式快取的實作

- **訊息佇列**：RabbitMQ、Apache Kafka  
  驗證非同步訊息處理、事件驅動架構

- **搜尋引擎**：Elasticsearch、Apache Solr  
  測試全文搜尋功能、資料索引邏輯

同一套生命週期 API 可以套用到不同服務，不必每換一種基礎設施就重新撰寫啟停流程。

相關連結：

- [Testcontainers - 官方網站](https://testcontainers.com/)
- [Testcontainers - GitHub](https://github.com/testcontainers)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Testcontainers for .NET / Getting Started](https://dotnet.testcontainers.org/getting-started/)
- [Testcontainers for .NET / Modules](https://dotnet.testcontainers.org/modules/)

## 環境準備

在開始使用 Testcontainers 之前，需要確保開發環境具備完整的容器化測試能力。

### 系統需求與安裝

#### Docker Desktop 環境

**最低系統需求**：

- Windows 10 版本 2004 或更新版本
- 啟用 WSL 2 功能
- 8GB RAM（建議 16GB 以上）
- 64GB 可用磁碟空間

**安裝步驟**：

1. **啟用 WSL 2**
   
   ```bash
   # 以系統管理員身分執行 PowerShell
   dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
   dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
   
   # 重新啟動電腦後執行
   wsl --set-default-version 2
   ```

2. **下載並安裝 Docker Desktop**
   - 前往 [Docker Desktop 官網](https://docs.docker.com/desktop/install/windows-install/)
   - 下載並執行安裝程式
   - 安裝時選擇「Use WSL 2 instead of Hyper-V」

3. **Docker Desktop 設定最佳化**
   
   ```json
   // Settings → Docker Engine
   {
     "builder": {
       "gc": {
         "defaultKeepStorage": "20GB",
         "enabled": true
       }
     }
   }
   ```

   **Resources 設定**：
   - Memory: 6GB（系統記憶體的 50-75%）
   - CPUs: 4 cores
   - Swap: 2GB
   - Disk image size: 64GB

#### .NET 開發環境

**安裝 .NET 10 SDK**：

```bash
# 檢查目前版本
dotnet --version

# 安裝全域工具
dotnet tool install --global dotnet-ef
dotnet tool install --global dotnet-reportgenerator-globaltool
```

### 環境驗證與測試

執行以下完整驗證流程確認環境正常：

#### 基礎環境檢查

```bash
# 檢查 Docker 是否正常運作
docker --version
docker run --rm hello-world

# 檢查 .NET SDK 版本
dotnet --version
dotnet --info

# 檢查可用的 Docker 映像檔
docker images
```

#### 資料庫容器測試

```bash
# 測試 PostgreSQL 容器
docker run --name test-postgres -e POSTGRES_PASSWORD=password -d -p 5432:5432 postgres:15-alpine
docker logs test-postgres
docker exec -it test-postgres psql -U postgres -c "SELECT version();"
docker stop test-postgres && docker rm test-postgres

# 測試 SQL Server 容器
docker run --name test-sqlserver -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=TestPass123!" -d -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
docker logs test-sqlserver
docker stop test-sqlserver && docker rm test-sqlserver
```

#### 外部服務容器測試

```bash
# 測試 Redis 容器
docker run --name test-redis -d -p 6379:6379 redis:7-alpine
docker exec -it test-redis redis-cli ping
docker stop test-redis && docker rm test-redis

# 測試 WireMock 容器
docker run --name test-wiremock -d -p 8080:8080 wiremock/wiremock:3.2.0
curl http://localhost:8080/__admin/health
docker stop test-wiremock && docker rm test-wiremock
```

### 專案套件準備

建立新的測試專案並安裝必要的 NuGet 套件：

```bash
# 建立解決方案和專案（名稱與本日 sample 一致）
dotnet new sln -n Day20.Samples
dotnet new classlib -n Day20.Core
dotnet new xunit -n Day20.Core.Integration.Tests

# 加入專案到解決方案
dotnet sln add Day20.Core
dotnet sln add Day20.Core.Integration.Tests
```

在測試專案中安裝套件。測試框架採 xUnit v3 + Microsoft.Testing.Platform（MTP），測試專案本身是可執行檔，`.csproj` 要加 `<OutputType>Exe</OutputType>`；`PackageReference` 只列名稱、不寫版本，版本統一集中在 per-day 的 `Directory.Packages.props`（CPM）：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <!-- 測試框架（xUnit v3 + Microsoft.Testing.Platform）-->
    <PackageReference Include="xunit.v3.mtp-v2" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
    <PackageReference Include="AwesomeAssertions" />

    <!-- Testcontainers 核心套件 -->
    <PackageReference Include="Testcontainers" />

    <!-- 資料庫容器 -->
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="Testcontainers.MsSql" />

    <!-- Entity Framework -->
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
</Project>
```

版本則集中寫在 `samples/day20/Directory.Packages.props`（本日 CPM 檔內容）：

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="AwesomeAssertions" Version="9.5.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.10" />
    <PackageVersion Include="Microsoft.Testing.Extensions.TrxReport" Version="2.3.3" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="StackExchange.Redis" Version="3.1.13" />
    <PackageVersion Include="Testcontainers" Version="4.13.0" />
    <PackageVersion Include="Testcontainers.MsSql" Version="4.13.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.13.0" />
    <PackageVersion Include="Testcontainers.Redis" Version="4.13.0" />
    <PackageVersion Include="WireMock.Net.Testcontainers" Version="2.14.0" />
    <PackageVersion Include="xunit.v3.mtp-v2" Version="3.2.2" />
  </ItemGroup>
</Project>
```

比起 xUnit v2，這裡拿掉了 `Microsoft.NET.Test.Sdk`、`xunit`、`xunit.runner.visualstudio`：它們的職責由 `xunit.v3.mtp-v2` 一次涵蓋。

### 常見問題處理

#### Docker 容器啟動失敗

- 檢查連接埠是否被佔用：`netstat -an | findstr :5432`
- 確認 Docker Desktop 正在執行
- 重新啟動 Docker Desktop 服務

#### 記憶體不足問題

- 調整 Docker Desktop 記憶體設定
- 清理未使用的映像檔：`docker system prune -a`
- 限制同時執行的容器數量

#### 網路連線問題

- 檢查企業防火牆設定
- 確認 Docker Desktop 網路模式設定
- 測試容器內外網路連通性

### 進階環境設定

#### .NET 工具與套件管理

**驗證安裝**：

```bash
# 檢查 .NET 版本
dotnet --version

# 列出已安裝的 SDK
dotnet --list-sdks

# 列出已安裝的執行階段
dotnet --list-runtimes

# 檢查 .NET 資訊
dotnet --info
```

**設定全域工具**：

```bash
# 安裝 Entity Framework Core 工具
dotnet tool install --global dotnet-ef

# 安裝測試報告工具
dotnet tool install --global dotnet-reportgenerator-globaltool

# 安裝程式碼涵蓋率工具
dotnet tool install --global dotnet-coverage

# 驗證工具安裝
dotnet tool list --global
```

#### NuGet 套件設定

建立 `NuGet.Config` 檔案來設定套件來源：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="dotnet-tools" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json" />
  </packageSources>
  <packageSourceCredentials />
</configuration>
```

#### Docker 環境驗證

建立簡單的驗證腳本 `verify-environment.ps1`：

```bash
# 檢查 Docker 狀態
Write-Host "Checking Docker version..." -ForegroundColor Green
docker --version

# 檢查 Docker Compose 版本
Write-Host "Checking Docker Compose version..." -ForegroundColor Green  
docker-compose --version

# 檢查 Docker 服務狀態
Write-Host "Checking Docker service status..." -ForegroundColor Green
docker system info --format "table {{.Name}}\t{{.Status}}"

# 測試容器啟動
Write-Host "Testing container startup..." -ForegroundColor Green
docker run --rm hello-world

# 檢查可用映像檔
Write-Host "Checking available images..." -ForegroundColor Green
docker images

# 檢查執行中的容器
Write-Host "Checking running containers..." -ForegroundColor Green
docker ps

# 檢查 Docker 資源使用情況
Write-Host "Checking Docker resource usage..." -ForegroundColor Green
docker system df

# 環境驗證完成
Write-Host "Environment verification completed!" -ForegroundColor Yellow
```

> **注意**：上述腳本使用英文註解，以避開 PowerShell 執行時的字元編碼問題。如果你複製後遇到編碼錯誤，可以把中文註解改成英文，或直接使用範例專案提供的 `verify-environment.ps1`。

**執行驗證步驟**：

1. 將上述腳本內容複製並儲存為 `verify-environment.ps1` 檔案
2. 開啟 PowerShell 並切換到腳本所在目錄
3. 執行以下指令：

```bash
# 方法一：直接執行腳本
.\verify-environment.ps1

# 方法二：如果遇到執行權限問題
powershell -ExecutionPolicy Bypass -File verify-environment.ps1
```

## 基本容器操作與 Wait Strategy

### 容器生命週期管理

Testcontainers 提供直觀的 API 來管理容器的完整生命週期，從建立、啟動到清理。

#### 為什麼使用 IAsyncLifetime？

在 Testcontainers 測試中，我們統一使用 xUnit 的 `IAsyncLifetime` 介面而不是 `IAsyncDisposable`，原因如下：

- **完整的生命週期控制**：`IAsyncLifetime` 用 `InitializeAsync()` 和 `DisposeAsync()` 分開初始化與清理邏輯
- **xUnit 官方建議**：這是 xUnit 測試框架推薦的非同步資源管理模式
- **測試隔離保證**：確保每個測試類別的容器都在測試開始前完全啟動，測試結束後完全清理
- **避免建構函式阻塞**：容器啟動等非同步操作移到 `InitializeAsync()` 中，避免在建構函式中進行同步等待

#### xUnit v3 的 IAsyncLifetime 簽章變更

xUnit v3 把 `IAsyncLifetime` 兩個方法的回傳型別從 `Task` 改成 `ValueTask`，而且 `DisposeAsync()` 現在直接繼承自 `IAsyncDisposable`：

```csharp
// xUnit v2
public interface IAsyncLifetime : IDisposable
{
    Task InitializeAsync();
    Task DisposeAsync();
}

// xUnit v3
public interface IAsyncLifetime : IAsyncDisposable
{
    ValueTask InitializeAsync();
    // DisposeAsync() 來自 IAsyncDisposable，同樣回傳 ValueTask
}
```

實務上遷移很單純：把測試類別裡 `InitializeAsync`／`DisposeAsync` 兩個方法的回傳型別從 `Task` 改成 `ValueTask` 就好，方法體一個字都不用動。本日 6 個 Testcontainers 測試類別（PostgreSql、Redis、SqlServer、UserServicePostgreSql、UserServiceSqlServer、WireMock）都只做了這個定式修正。

#### xUnit1051：真實呼叫要傳 CancellationToken

xUnit v3 內建一條分析規則 xUnit1051：測試方法裡呼叫「有 `CancellationToken` 多載」的非同步方法卻沒傳 token，就會跳警告，要你改用 `TestContext.Current.CancellationToken`。整合測試幾乎全是真實呼叫——EF Core 的 `SaveChangesAsync`／`ToListAsync`／`CountAsync`／`FirstOrDefaultAsync`／`CanConnectAsync`／`BeginTransactionAsync`，以及 WireMock 測試裡的 `HttpClient` 呼叫與 `Task.Delay`——本日一共補了 34 處 token。

有兩個細節值得記一下：

- **`StackExchange.Redis` 的 API 不吃 `CancellationToken`**，所以 Redis 測試一處都不用補，analyzer 也不會標。
- **`Assert.ThrowsAsync(() => ...)` 的 lambda 引數 analyzer 會追進去**：lambda 裡的真實呼叫（例如 `() => context.SaveChangesAsync()`）一樣會被標 xUnit1051，一樣要補上 token。

`InitializeAsync`／`DisposeAsync` 本身不是測試方法，analyzer 不標，就不用動。

#### 基本容器建立模式

```csharp
public class BasicContainerOperationsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public BasicContainerOperationsTests()
    {
        _postgres = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithPortBinding(5432, true)  // 自動分配主機埠號
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        // 啟動容器並等待就緒
        await _postgres.StartAsync();
    }

    [Fact]
    public async Task GetConnectionString_容器啟動後_應提供有效連線字串()
    {
        // Arrange & Act
        var connectionString = _postgres.GetConnectionString();
        var mappedPort = _postgres.GetMappedPublicPort(5432);

        // Assert
        connectionString.Should().NotBeNullOrEmpty();
        connectionString.Should().Contain($"Port={mappedPort}");
        connectionString.Should().Contain("Database=testdb");
        connectionString.Should().Contain("Username=testuser");
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

### Wait Strategy 最佳實務

Wait Strategy 確保容器完全啟動後才執行測試，這是穩定測試的關鍵。

#### 內建 Wait Strategy

```csharp
// 等待特定埠號可用
var postgres = new PostgreSqlBuilder("postgres:15-alpine")
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilInternalTcpPortIsAvailable(5432))
    .Build();

// 等待 HTTP 端點回應
var webApi = new ContainerBuilder()
    .WithImage("nginx:alpine")
    .WithPortBinding(80, true)
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilHttpRequestIsSucceeded(r => r.ForPort(80).ForPath("/")))
    .Build();

// 等待日誌訊息出現
var redis = new ContainerBuilder()
    .WithImage("redis:7-alpine")
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilMessageIsLogged("Ready to accept connections"))
    .Build();
```

#### 複合 Wait Strategy

```csharp
var sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
    .WithPassword("YourStrong@Passw0rd")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilInternalTcpPortIsAvailable(1433)
        .UntilMessageIsLogged("SQL Server is now ready for client connections"))
    .Build();
```

#### 自訂 Wait Strategy

```csharp
public class DatabaseReadyWaitStrategy : IWaitUntil
{
    private readonly string _connectionString;

    public DatabaseReadyWaitStrategy(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> UntilAsync(IContainer container)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// 使用自訂 Wait Strategy
var postgres = new PostgreSqlBuilder("postgres:15-alpine")
    .WithWaitStrategy(new DatabaseReadyWaitStrategy(_postgres.GetConnectionString()))
    .Build();
```

### 容器網路與連接埠管理

#### 動態埠號分配

```csharp
[Fact]
public async Task GetMappedPublicPort_使用隨機埠號_應回傳不同於預設埠號()
{
    // Arrange
    var redis = new ContainerBuilder()
        .WithImage("redis:7-alpine")
        .WithPortBinding(6379, true)  // 使用隨機埠號
        .Build();

    // Act
    await redis.StartAsync();
    var mappedPort = redis.GetMappedPublicPort(6379);

    // Assert
    mappedPort.Should().BeGreaterThan(1024);
    mappedPort.Should().NotBe(6379);  // 應該不是預設埠號
}
```

#### 固定埠號分配（測試環境）

```csharp
[Fact]
public async Task GetMappedPublicPort_使用固定埠號映射_應回傳指定埠號()
{
    // Arrange
    var postgres = new PostgreSqlBuilder("postgres:15-alpine")
        .WithPortBinding(15432, 5432)  // 固定映射到 15432
        .Build();

    // Act
    await postgres.StartAsync();
    var mappedPort = postgres.GetMappedPublicPort(5432);

    // Assert
    mappedPort.Should().Be(15432);
}
```

### 容器資源限制與效能調整

Testcontainers 沒有專用的資源限制方法，資源上限要透過 `WithCreateParameterModifier` 直接改 Docker 的 `HostConfig`（型別來自 `Docker.DotNet.Models`）：

```csharp
using Docker.DotNet.Models;

var postgres = new PostgreSqlBuilder("postgres:15-alpine")
    .WithCreateParameterModifier(parameters =>
    {
        parameters.HostConfig.Memory = 512L * 1024 * 1024;  // 記憶體上限 512MB
        parameters.HostConfig.NanoCPUs = 1_000_000_000;     // CPU 上限 1 核（1 核 = 1e9 奈秒）
    })
    .WithTmpfsMount("/var/lib/postgresql/data")  // 使用記憶體檔案系統
    .Build();
```

> 詳細參數可參考 [Testcontainers for .NET — Advanced 設定](https://dotnet.testcontainers.org/api/create_docker_container/) 與 Docker Engine 的 HostConfig 文件。

### 容器日誌管理

```csharp
[Fact]
public async Task GetLogsAsync_容器啟動後_應包含資料庫準備完成訊息()
{
    // Arrange
    var postgres = new PostgreSqlBuilder("postgres:15-alpine")
        .Build();

    // Act
    await postgres.StartAsync();
    
    // 取得容器日誌
    var logs = await postgres.GetLogsAsync();

    // Assert
    logs.Stdout.Should().Contain("database system is ready to accept connections");
    logs.Stderr.Should().BeEmpty();
}
```

這幾個操作是 Testcontainers 的基本功，容器能不能穩定起來就看它們。

## 資料庫整合測試

資料庫是應用程式的核心相依性。Testcontainers 讓測試直接連到真實資料庫引擎。

### PostgreSQL 容器測試

PostgreSQL 是目前最多人用的開源關聯式資料庫。

#### 基本 PostgreSQL 測試設定

```csharp
using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using AwesomeAssertions;

public class UserServicePostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private UserDbContext _dbContext = null!;
    private SqlUserService _userService = null!;

    public UserServicePostgreSqlTests()
    {
        _container = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithPortBinding(54321, true)
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        // 啟動容器
        await _container.StartAsync();

        // 設定 DbContext
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        _dbContext = new UserDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _userService = new SqlUserService(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task CreateUserAsync_輸入有效使用者資料_應成功建立使用者()
    {
        // Arrange
        var request = new UserCreateRequest
        {
            Username = "testuser_postgres",
            FullName = "Test User_postgres",
            Email = "test_postgres@example.com",
            Age = 25
        };

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
        result.Username.Should().Be(request.Username);
        result.FullName.Should().Be(request.FullName);
        result.Email.Should().Be(request.Email);
        result.Age.Should().Be(request.Age);
    }
}
```

### SQL Server 容器測試

SQL Server 是 Microsoft 的企業級關聯式資料庫，廣泛用於企業環境。

#### SQL Server 容器設定與測試

```csharp
using Testcontainers.MsSql;
using Microsoft.EntityFrameworkCore;
using AwesomeAssertions;

public class UserServiceSqlServerTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    private UserDbContext _dbContext = null!;
    private SqlUserService _userService = null!;

    public UserServiceSqlServerTests()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("TestPass123!")
            .WithPortBinding(15433, true)
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        // 啟動容器
        await _container.StartAsync();

        // 設定 DbContext
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        _dbContext = new UserDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _userService = new SqlUserService(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task CreateUserAsync_輸入有效使用者資料_應成功建立使用者()
    {
        // Arrange
        var request = new UserCreateRequest
        {
            Username = "testuser_sqlserver",
            FullName = "Test User_sqlserver",
            Email = "test_sqlserver@example.com",
            Age = 25
        };

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
        result.Username.Should().Be(request.Username);
        result.FullName.Should().Be(request.FullName);
        result.Email.Should().Be(request.Email);
        result.Age.Should().Be(request.Age);
    }

    [Fact]
    public async Task GetUserByIdAsync_輸入已存在的使用者ID_應回傳對應使用者()
    {
        // Arrange
        var createRequest = new UserCreateRequest
        {
            Username = "testuser2_sqlserver",
            FullName = "Test User2_sqlserver",
            Email = "test2_sqlserver@example.com",
            Age = 25
        };
        var createdUser = await _userService.CreateUserAsync(createRequest);

        // Act
        var result = await _userService.GetUserByIdAsync(createdUser.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(createdUser.Id);
        result.Username.Should().Be(createRequest.Username);
        result.FullName.Should().Be(createRequest.FullName);
        result.Email.Should().Be(createRequest.Email);
    }

}
```

### Entity Framework Core 整合測試

在整合測試中，我們需要建立 DbContext 來處理資料庫操作。以下是我們的資料模型設計：

#### UserDbContext 類別

```csharp
using Day20.Core.Models;

namespace Day20.Core.Data;

/// <summary>
/// SQL 資料庫 DbContext (支援 PostgreSQL 和 SQL Server)
/// </summary>
public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 使用者資料集
    /// </summary>
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            // 設定主鍵
            entity.HasKey(e => e.Id);

            // 設定索引
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();

            // 設定屬性
            entity.Property(e => e.Id)
                  .HasMaxLength(36)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.Username)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Email)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.FullName)
                  .HasMaxLength(100)
                  .IsRequired();

            // 根據資料庫提供者設定預設值
            if (Database.IsNpgsql())
            {
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("NOW()"); // PostgreSQL
            }
            else if (Database.IsSqlServer())
            {
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()"); // SQL Server
            }
            else
            {
                // 其他資料庫或沒有預設值
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValue(DateTime.UtcNow);
            }

            // 種子資料
            entity.HasData(
                new User
                {
                    Id = "1",
                    Username = "admin",
                    Email = "admin@example.com",
                    FullName = "系統管理員",
                    Age = 30,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = "2",
                    Username = "testuser",
                    Email = "test@example.com",
                    FullName = "測試使用者",
                    Age = 25,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            );
        });
    }
}
```

#### User 實體類別

```csharp
namespace Day20.Core.Models;

/// <summary>
/// 使用者實體 - 支援多種資料庫
/// </summary>
public class User
{
    /// <summary>
    /// 使用者識別碼
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 使用者名稱
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 電子郵件
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 全名
    /// </summary>
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// 年齡
    /// </summary>
    [Range(1, 150)]
    public int Age { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新時間
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
```

#### 請求模型類別

```csharp
namespace Day20.Core.Models;

/// <summary>
/// 建立使用者請求
/// </summary>
public class UserCreateRequest
{
    /// <summary>
    /// 使用者名稱
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 電子郵件
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 全名
    /// </summary>
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// 年齡
    /// </summary>
    [Range(1, 150)]
    public int Age { get; set; }
}
```

### 複雜查詢測試

```csharp
[Fact]
public async Task ExecuteComplexQuery_執行聚合查詢_應正確計算分類統計()
{
    // Arrange
    var electronics = new Category { Name = "Electronics" };
    var books = new Category { Name = "Books" };
    
    await _dbContext.Categories.AddRangeAsync(electronics, books);
    await _dbContext.SaveChangesAsync();

    var products = new[]
    {
        new Product { Name = "筆電", Price = 30000m, CategoryId = electronics.Id },
        new Product { Name = "手機", Price = 20000m, CategoryId = electronics.Id },
        new Product { Name = "C# 程式設計", Price = 500m, CategoryId = books.Id },
        new Product { Name = "測試驅動開發", Price = 600m, CategoryId = books.Id }
    };

    await _dbContext.Products.AddRangeAsync(products);
    await _dbContext.SaveChangesAsync();

    // Act
    var categoryStats = await _dbContext.Categories
        .Select(c => new
        {
            CategoryName = c.Name,
            ProductCount = _dbContext.Products.Count(p => p.CategoryId == c.Id),
            AveragePrice = _dbContext.Products
                .Where(p => p.CategoryId == c.Id)
                .Average(p => p.Price),
            TotalValue = _dbContext.Products
                .Where(p => p.CategoryId == c.Id)
                .Sum(p => p.Price)
        })
        .ToListAsync();

    // Assert
    categoryStats.Should().HaveCount(2);

    var electronicsStats = categoryStats.First(s => s.CategoryName == "Electronics");
    electronicsStats.ProductCount.Should().Be(2);
    electronicsStats.AveragePrice.Should().Be(25000m);
    electronicsStats.TotalValue.Should().Be(50000m);

    var booksStats = categoryStats.First(s => s.CategoryName == "Books");
    booksStats.ProductCount.Should().Be(2);
    booksStats.AveragePrice.Should().Be(550m);
    booksStats.TotalValue.Should().Be(1100m);
}
```

這些測試展示了如何使用 Testcontainers 進行真實的資料庫整合測試，涵蓋基本 CRUD 操作、約束驗證、交易處理和複雜查詢等情境。

### SQL Server 進階設定範例

SQL Server 是常見的企業級關聯式資料庫。以下示範較進階的設定與測試：

```csharp
public class AdvancedSqlServerTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer;
    private UserDbContext _dbContext = null!;
    private SqlUserService _userService = null!;

    public AdvancedSqlServerTests()
    {
        // 建構式只負責 Build，不做任何啟動（避免建構式阻塞）
        _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("TestPass123!")           // SQL Server 需要強密碼
            .WithEnvironment("ACCEPT_EULA", "Y")    // 接受授權條款
            .WithPortBinding(15433, true)           // 自動分配埠號
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(1433))        // 等待埠號可用
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        // 啟動容器與初始化資料庫都放在這裡（非同步、不阻塞）
        await _sqlServer.StartAsync();

        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseSqlServer(_sqlServer.GetConnectionString())
            .Options;

        _dbContext = new UserDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _userService = new SqlUserService(_dbContext);
    }

    [Fact]
    public async Task CanConnectAsync_資料庫連線_應回傳健康狀態()
    {
        // Act & Assert
        (await _dbContext.Database.CanConnectAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task RollbackTransaction_建立使用者後復原_應不存在於資料庫()
    {
        // Arrange
        var request = new UserCreateRequest 
        { 
            Username = "transactiontest", 
            FullName = "Transaction Test",
            Email = "transaction@example.com",
            Age = 30
        };

        // Act
        using var transaction = await _dbContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        
        var createdUser = await _userService.CreateUserAsync(request);
        
        // 復原交易
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        // Assert
        var userCount = await _dbContext.Users.CountAsync(TestContext.Current.CancellationToken);
        userCount.Should().Be(0);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}
```

這個 SQL Server 範例加入**交易復原測試**，驗證交易失敗後資料是否真的回復。InMemory 資料庫無法重現這項行為，因此必須對真實 SQL Server 容器執行。

## 外部服務模擬測試

微服務通常還會連到 HTTP API、快取與訊息佇列。Testcontainers 可以把這些服務一併納入測試環境。

### WireMock HTTP API 模擬

WireMock 用來模擬外部 HTTP API，可以設定回應內容、狀態碼、延遲與錯誤情境。

#### 簡單的入門範例

先看一個簡單的 WireMock 範例：

```csharp
using Day20.Core.Services;
using Day20.Core.Services.Implementations;
using AwesomeAssertions;
using WireMock.Net.Testcontainers;

public class ExternalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ExternalApiClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public async Task<UserProfile?> GetUserProfileAsync(int userId)
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/users/{userId}");
        
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UserProfile>(json);
    }
}

public class ExternalApiTests : IAsyncLifetime
{
    private readonly WireMockContainer _wireMockContainer = new WireMockContainerBuilder()
        .Build();

    private IExternalApiService _externalApiService = null!;
    private HttpClient _httpClient = null!;

    public async ValueTask InitializeAsync()
    {
        await _wireMockContainer.StartAsync();

        _httpClient = new HttpClient();
        // WireMock 預設使用 port 80
        var baseUrl = $"http://localhost:{_wireMockContainer.GetMappedPublicPort(80)}";
        _externalApiService = new ExternalApiService(_httpClient, baseUrl, baseUrl);
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        await _wireMockContainer.DisposeAsync();
    }

    [Fact]
    public async Task ValidateEmailAsync_使用有效電子郵件_應回傳True()
    {
        // Arrange
        var email = "test@example.com";

        // 最簡單可行的 mapping - 回到工作的版本
        var mappingJson = """
                          {
                            "request": {
                              "method": "GET",
                              "urlPath": "/api/email/validate",
                              "queryParameters": {
                                "email": {
                                  "equalTo": "test@example.com"
                                }
                              }
                            },
                            "response": {
                              "status": 200,
                              "headers": {
                                "Content-Type": "application/json"
                              },
                              "body": "{\"IsValid\": true, \"Message\": \"Email is valid\"}"
                            }
                          }
                          """;

        // 使用 HttpClient 直接設定 WireMock mapping
        var adminUrl = $"http://localhost:{_wireMockContainer.GetMappedPublicPort(80)}/__admin/mappings";
        var content = new StringContent(mappingJson, Encoding.UTF8, "application/json");
        var mappingResponse = await _httpClient.PostAsync(adminUrl, content, TestContext.Current.CancellationToken);
        mappingResponse.EnsureSuccessStatusCode();

        // 等待一點時間讓 mapping 生效
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act
        var result = await _externalApiService.ValidateEmailAsync(email, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }
}
```

#### 基本 WireMock 設定

```csharp
using System.Text;
using Day20.Core.Services.Implementations;
using DotNet.Testcontainers.Containers;
using WireMock.Net.Testcontainers;

public class WireMockIntegrationTests : IAsyncLifetime
{
    private readonly WireMockContainer _wireMockContainer = new WireMockContainerBuilder().Build();

    private readonly ITestOutputHelper _output;
    private IExternalApiService _externalApiService = null!;
    private HttpClient _httpClient = null!;

    /// <summary>
    /// 建構式，注入 ITestOutputHelper 用於測試輸出
    /// </summary>
    /// <param name="output"></param>
    public WireMockIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 測試初始化
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        await _wireMockContainer.StartAsync();

        _httpClient = new HttpClient();
        // WireMock 預設使用 port 80
        var baseUrl = $"http://localhost:{_wireMockContainer.GetMappedPublicPort(80)}";
        _externalApiService = new ExternalApiService(_httpClient, baseUrl, baseUrl);
    }

    /// <summary>
    /// 測試清理
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        await _wireMockContainer.DisposeAsync();
    }

    [Fact]
    public async Task ValidateEmailAsync_使用有效電子郵件_應回傳True()
    {
        // Arrange
        var email = "test@example.com";

        // 最簡單可行的 mapping - 回到工作的版本
        var mappingJson = """
                          {
                            "request": {
                              "method": "GET",
                              "urlPath": "/api/email/validate",
                              "queryParameters": {
                                "email": {
                                  "equalTo": "test@example.com"
                                }
                              }
                            },
                            "response": {
                              "status": 200,
                              "headers": {
                                "Content-Type": "application/json"
                              },
                              "body": "{\"IsValid\": true, \"Message\": \"Email is valid\"}"
                            }
                          }
                          """;

        // 使用 HttpClient 直接設定 WireMock mapping
        var adminUrl = $"http://localhost:{_wireMockContainer.GetMappedPublicPort(80)}/__admin/mappings";
        var content = new StringContent(mappingJson, Encoding.UTF8, "application/json");
        var mappingResponse = await _httpClient.PostAsync(adminUrl, content, TestContext.Current.CancellationToken);
        mappingResponse.EnsureSuccessStatusCode();

        // 等待一點時間讓 mapping 生效
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act
        var result = await _externalApiService.ValidateEmailAsync(email, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateEmailAsync_使用無效電子郵件_應回傳False()
    {
        // Arrange
        var email = "invalid-email";
        var mappingJson = """
                          {
                            "request": {
                              "method": "GET",
                              "urlPath": "/api/email/validate",
                              "queryParameters": {
                                "email": {
                                  "equalTo": "invalid-email"
                                }
                              }
                            },
                            "response": {
                              "status": 200,
                              "headers": {
                                "Content-Type": "application/json"
                              },
                              "body": "{\"IsValid\": false, \"Message\": \"Email is invalid\"}"
                            }
                          }
                          """;

        var adminUrl = $"http://localhost:{_wireMockContainer.GetMappedPublicPort(80)}/__admin/mappings";
        var content = new StringContent(mappingJson, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(adminUrl, content, TestContext.Current.CancellationToken);

        // Act
        var result = await _externalApiService.ValidateEmailAsync(email, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }
}
```

### Redis 快取服務測試

Redis 是廣泛使用的記憶體資料結構儲存系統，常用作快取、訊息佇列等。

#### Redis 容器整合測試

```csharp
using StackExchange.Redis;
using Testcontainers.Redis;
using AwesomeAssertions;

public class RedisIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redis;
    private IConnectionMultiplexer _connection = null!;
    private IDatabase _database = null!;

    public RedisIntegrationTests()
    {
        _redis = new RedisBuilder("redis:7-alpine")
            .WithCleanUp(true)
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _database = _connection.GetDatabase();
    }

    [Fact]
    public async Task StringSetAsync_設定字串值_應正確儲存和讀取()
    {
        // Arrange
        const string key = "test:user:123";
        const string value = "測試使用者資料";

        // Act
        await _database.StringSetAsync(key, value);
        var retrievedValue = await _database.StringGetAsync(key);

        // Assert
        retrievedValue.Should().Be(value);
    }

    [Fact]
    public async Task StringSetAsync_設定過期時間_應能正確設定TTL()
    {
        // Arrange
        const string key = "test:session:456";
        const string value = "session_data";
        var expiry = TimeSpan.FromSeconds(10); // 使用較長的過期時間

        // Act
        await _database.StringSetAsync(key, value, expiry);
        var immediateValue = await _database.StringGetAsync(key);
        var ttl = await _database.KeyTimeToLiveAsync(key);

        // Assert
        immediateValue.Should().Be(value);
        ttl.Should().NotBeNull();
        ttl.Value.TotalSeconds.Should().BeGreaterThan(0);
        ttl.Value.TotalSeconds.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task HashSetAsync_設定雜湊結構_應正確操作多個欄位()
    {
        // Arrange
        const string key = "user:profile:789";
        var userHash = new HashEntry[]
        {
            new("name", "張三"),
            new("email", "zhang@example.com"),
            new("age", "30")
        };

        // Act
        await _database.HashSetAsync(key, userHash);
        var name = await _database.HashGetAsync(key, "name");
        var email = await _database.HashGetAsync(key, "email");
        var age = await _database.HashGetAsync(key, "age");

        // Assert
        name.Should().Be("張三");
        email.Should().Be("zhang@example.com");
        age.Should().Be("30");
    }

    public async ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        await _redis.DisposeAsync();
    }
}
```

### 快取服務層測試

```csharp
/// <summary>
/// 快取服務介面
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// 取得快取值
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// 設定快取值
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;

    /// <summary>
    /// 刪除快取值
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// 檢查快取鍵是否存在
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 清除所有快取
    /// </summary>
    Task ClearAllAsync();
}

/// <summary>
/// Redis 快取服務實作
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    /// <summary>
    /// 取得快取值
    /// </summary>
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = await _database.StringGetAsync(key);
        if (!value.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(value.ToString());
    }

    /// <summary>
    /// 設定快取值
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        var serializedValue = JsonSerializer.Serialize(value);
        await _database.StringSetAsync(key, serializedValue, expiry.HasValue ? new Expiration(expiry.Value) : default(Expiration));
    }

    /// <summary>
    /// 刪除快取值
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        await _database.KeyDeleteAsync(key);
    }

    /// <summary>
    /// 檢查快取鍵是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string key)
    {
        return await _database.KeyExistsAsync(key);
    }

    /// <summary>
    /// 清除所有快取
    /// </summary>
    public async Task ClearAllAsync()
    {
        var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
        await server.FlushDatabaseAsync();
    }
}

public class CacheServiceIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redis;
    private IConnectionMultiplexer _connection = null!;
    private ICacheService _cacheService = null!;

    public CacheServiceIntegrationTests()
    {
        _redis = new RedisBuilder("redis:7-alpine")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _cacheService = new RedisCacheService(_connection.GetDatabase());
    }

    [Fact]
    public async Task SetAsync_序列化使用者物件_應正確序列化和反序列化()
    {
        // Arrange
        var user = new User 
        { 
            Id = Guid.NewGuid(),
            Name = "測試使用者", 
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };

        const string key = "user:cache:test";

        // Act
        await _cacheService.SetAsync(key, user);
        var cachedUser = await _cacheService.GetAsync<User>(key);

        // Assert
        cachedUser.Should().NotBeNull();
        cachedUser!.Id.Should().Be(user.Id);
        cachedUser.Name.Should().Be(user.Name);
        cachedUser.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetAsync_輸入不存在的Key_應回傳Null()
    {
        // Act
        var result = await _cacheService.GetAsync<User>("nonexistent:key");
        var exists = await _cacheService.ExistsAsync("nonexistent:key");

        // Assert
        result.Should().BeNull();
        exists.Should().BeFalse();
    }

    public async ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        await _redis.DisposeAsync();
    }
}
```

這些測試在隔離環境中驗證應用程式與外部服務的整合邏輯，包括正常回應、錯誤與逾時情境。

## 總結

本篇的 Testcontainers 範例涵蓋：

**學習重點回顧**：

1. **Testcontainers 框架介紹**
   - 了解容器化測試的優勢與價值
   - 掌握與傳統 Mock 測試的差異
   - 認識 .NET Testcontainers 生態系

2. **基本容器操作**
   - 容器的建立、啟動和銷毀流程
   - 連接埠映射與環境變數設定
   - 使用 `IAsyncLifetime` 管理容器生命週期（xUnit v3 的 `DisposeAsync` 即來自 `IAsyncDisposable`）

3. **基礎資料庫整合測試**
   - PostgreSQL 和 SQL Server 容器的設定與使用
   - 真實資料庫環境下的基本 Entity Framework Core 測試
   - 資料庫連線和基本 CRUD 操作驗證

4. **外部服務的基礎模擬**
   - 使用 WireMock 容器模擬簡單的外部 API
   - 建立可控制的測試環境
   - 實現基本的服務互動測試

**關鍵優勢**：

- **真實環境測試**：使用實際的資料庫和服務，不再依賴模擬
- **環境一致性**：開發、測試、正式環境使用相同版本
- **自動化管理**：容器的建立、設定和清理完全自動化
- **測試隔離性**：每個測試都有獨立、乾淨的環境

**最佳實務**：

- 每個測試類別使用獨立的容器執行個體，確保測試隔離
- 善用 Wait Strategy 確保容器完全啟動後才執行測試
- 適當設定容器資源限制，避免測試環境資源不足
- 正確實作 `IAsyncLifetime` 來管理容器生命週期（xUnit v3 的 `DisposeAsync` 即來自 `IAsyncDisposable`）

**實作要點**：

- 容器生命週期管理：建構函式建立 container definition、`InitializeAsync()` 啟動、`DisposeAsync()` 清理
- 連線字串動態取得：`container.GetConnectionString()`
- 埠號自動分配：避免測試間的埠號衝突
- 資源清理：確保測試後容器被正確清理

完成這些範例後，測試已能涵蓋真實服務的啟動、連線、資料隔離與清理。下一步要依 CI 資源與測試數量，決定容器的共用範圍。

## 在本機執行測試（MTP）

xUnit v3 走 Microsoft Testing Platform（MTP），runner 由本日 sample 的 `global.json` 指定。**務必先切換到該 sample 目錄再執行**，否則不會套用 per-day `global.json`，`dotnet test` 會落回 VSTest 而失敗；也不要從 repository root 直接指定子目錄 solution：

```powershell
Set-Location samples/day20
dotnet test --solution Day20.Samples.sln -c Release
```

## 參考資料

### 官方文件與資源

- [Testcontainers 官方網站](https://testcontainers.com/)
- [Testcontainers for .NET 官方文件](https://dotnet.testcontainers.org/)
- [Testcontainers GitHub 組織](https://github.com/testcontainers/)
- [Testcontainers for .NET 入門指南](https://testcontainers.com/guides/getting-started-with-testcontainers-for-dotnet/)
- [Getting Started with Testcontainers for .NET - GitHub 範例](https://github.com/testcontainers/tc-guide-getting-started-with-testcontainers-for-dotnet)

### Docker Desktop 與 WSL2 相關文件

- [Docker Desktop for Windows 安裝指南](https://docs.docker.com/desktop/setup/install/windows-install/)
- [WSL 2 安裝與設定](https://learn.microsoft.com/zh-tw/windows/wsl/install)
- [Docker Desktop WSL 2 整合](https://docs.docker.com/desktop/features/wsl/)

### 工具與資源

- [Visual Studio Code](https://code.visualstudio.com/download)
- [Windows Terminal](https://learn.microsoft.com/zh-tw/windows/terminal/install)
- [WSL GitHub Releases](https://github.com/microsoft/WSL/releases)

### 技術文章

- [點部落 - Repository 測試使用 Testcontainers - mrkt](https://dotblogs.com.tw/mrkt/2023/10/13/151915)
- [點部落 - Repository 測試使用 Testcontainers - 原始碼 - mrkt](https://dotblogs.com.tw/mrkt/2024/03/01/130003)

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day20>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第二十天。明天會介紹 Day 21 - Testcontainers 整合測試：MSSQL + EF Core 以及 Dapper 基礎應用。**
