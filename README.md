# 老派軟體工程師的測試修練 — 30 天挑戰（.NET 10 版）

「重啟挑戰：老派軟體工程師的測試修練」系列的 .NET 10 更新版。文章與範例專案全面升級到 .NET 10 與 xUnit v3（Microsoft Testing Platform 模式），每一篇的程式碼都對應一個可直接建置、執行的範例專案。

目前發佈 Day 01～18，其餘各天陸續整理中。

## 原版系列（2025 iThome 鐵人賽）

本系列改寫自 2025 iThome 鐵人賽「重啟挑戰：老派軟體工程師的測試修練」，原版以 .NET 9 撰寫：

- 系列文章：[重啟挑戰：老派軟體工程師的測試修練](https://ithelp.ithome.com.tw/users/20066083/ironman/8276)
- 原版範例程式：[kevintsengtw/30Days_in_Testing_Samples](https://github.com/kevintsengtw/30Days_in_Testing_Samples)

## 文章清單

| 天數 | 文章 | 範例專案 |
| ------ | ------ | ---------- |
| Day 01 | [老派工程師的測試啟蒙 - 為什麼我們需要測試？](Day01.md) | [samples/day01](samples/day01) |
| Day 02 | [xUnit 框架深度解析 - 從生態概觀到實戰專案](Day02.md) | [samples/day02](samples/day02) |
| Day 03 | [xUnit 進階功能與測試資料管理](Day03.md) | [samples/day03](samples/day03) |
| Day 04 | [AwesomeAssertions 基礎應用與實戰技巧](Day04.md) | [samples/day04](samples/day04) |
| Day 05 | [AwesomeAssertions 進階技巧與複雜情境應用](Day05.md) | [samples/day05](samples/day05) |
| Day 06 | [Code Coverage 程式碼涵蓋範圍實戰指南](Day06.md) | [samples/day06](samples/day06) |
| Day 07 | [相依替代入門 - 使用 NSubstitute](Day07.md) | [samples/day07](samples/day07) |
| Day 08 | [測試輸出與記錄 - xUnit ITestOutputHelper 與 ILogger](Day08.md) | [samples/day08](samples/day08) |
| Day 09 | [測試私有與內部成員 - Private 與 Internal 的測試策略](Day09.md) | [samples/day09](samples/day09) |
| Day 10 | [AutoFixture 基礎：自動產生測試資料](Day10.md) | [samples/day10](samples/day10) |
| Day 11 | [AutoFixture 進階：自訂化測試資料產生策略](Day11.md) | [samples/day11](samples/day11) |
| Day 12 | [結合 AutoData：xUnit 與 AutoFixture 的整合應用](Day12.md) | [samples/day12](samples/day12) |
| Day 13 | [NSubstitute 與 AutoFixture 的整合應用](Day13.md) | [samples/day13](samples/day13) |
| Day 14 | [Bogus 入門：與 AutoFixture 的差異比較](Day14.md) | [samples/day14](samples/day14) |
| Day 15 | [AutoFixture 與 Bogus 的整合應用](Day15.md) | [samples/day15](samples/day15) |
| Day 16 | [測試日期與時間：Microsoft.Bcl.TimeProvider 取代 DateTime](Day16.md) | [samples/day16](samples/day16) |
| Day 17 | [檔案與 IO 測試：使用 System.IO.Abstractions 模擬檔案系統 - 打造可測試的檔案操作](Day17.md) | [samples/day17](samples/day17) |
| Day 18 | [驗證測試：FluentValidation Test Extensions](Day18.md) | [samples/day18](samples/day18) |

## 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 任一開發工具：Visual Studio 2022 以上、JetBrains Rider、VS Code

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
