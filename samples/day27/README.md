# Day 27：TUnit 入門

這個範例使用 .NET 10、TUnit 與 Microsoft Testing Platform，展示基本測試、`[Arguments]`、awaitable assertions、TimeProvider、生命週期與並行控制。

## 專案結構

```text
samples/day27/
├── Day27.TUnit.sln
├── Directory.Packages.props
├── global.json
├── src/TUnit.Demo.Core/
└── tests/TUnit.Demo.Tests/
```

## 執行

```powershell
dotnet test --solution Day27.TUnit.sln
```

需要 coverage 與 TRX 時，可以直接執行測試專案：

```powershell
dotnet run --project tests/TUnit.Demo.Tests --configuration Release --coverage --report-trx
```

## 驗證基準

- Target framework：`net10.0`
- TUnit：版本由本目錄的 `Directory.Packages.props` 管理
- 測試數：48
- `Microsoft.NET.Test.Sdk`：不使用

## 對應文章

- `Day27.new.md`
