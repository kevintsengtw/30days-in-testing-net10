# Day 29：TUnit 執行控制、ASP.NET Core 與 Testcontainers

本日分成兩個測試專案：

- `TUnit.Advanced.ExecutionControl.Tests`：Retry、Timeout、DisplayName、Properties。
- `TUnit.Advanced.Integration.Tests`：TUnit.AspNetCore、PostgreSQL、Redis、Kafka。

## 專案結構

```text
samples/day29/
├── Day29.TUnitAdvanced.sln
├── Directory.Packages.props
├── global.json
├── src/
│   ├── TUnit.Advanced.Core/
│   └── TUnit.Advanced.WebApi/
└── tests/
    ├── TUnit.Advanced.ExecutionControl.Tests/
    └── TUnit.Advanced.Integration.Tests/
```

## 執行

不需要 Docker 的測試：

```powershell
dotnet test --project tests/TUnit.Advanced.ExecutionControl.Tests
```

需要 Docker 的整合測試：

```powershell
docker info
dotnet test --project tests/TUnit.Advanced.Integration.Tests
```

完整 solution：

```powershell
dotnet test --solution Day29.TUnitAdvanced.sln
```

## 測試過濾

```powershell
dotnet test --project tests/TUnit.Advanced.ExecutionControl.Tests `
  --treenode-filter "/*/*/*/*[Suite=Smoke]"
```

## 驗證基準

- Target framework：`net10.0`
- ExecutionControl：16 tests
- Integration：23 tests
- Docker Desktop 驗證版本：29.6.2

## 對應文章

- `Day29.new.md`
