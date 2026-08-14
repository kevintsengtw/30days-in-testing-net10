# Day 28：TUnit 資料來源、生命週期與 Dependency Injection

本日分成兩個測試專案：

- `TUnit.Advanced.DataDriven.Tests`：MethodDataSource、ClassDataSource、Matrix Tests。
- `TUnit.Advanced.Lifecycle.Tests`：Properties、hooks、disposal 與 Microsoft DI。

## 專案結構

```text
samples/day28/
├── Day28.TUnitAdvanced.sln
├── Directory.Packages.props
├── global.json
├── src/TUnit.Advanced.Core/
└── tests/
    ├── TUnit.Advanced.DataDriven.Tests/
    └── TUnit.Advanced.Lifecycle.Tests/
```

## 執行

```powershell
dotnet test --solution Day28.TUnitAdvanced.sln
```

分開執行：

```powershell
dotnet test --project tests/TUnit.Advanced.DataDriven.Tests
dotnet test --project tests/TUnit.Advanced.Lifecycle.Tests
```

## 驗證基準

- Target framework：`net10.0`
- 測試數：149
- 外部服務：不需要

## 對應文章

- `Day28.new.md`
