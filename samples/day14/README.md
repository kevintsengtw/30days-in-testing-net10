# Day 14 - Bogus 資料產生範例專案

這個專案展示如何使用 Bogus 函式庫產生各種測試資料，並與 AutoFixture 進行比較。專案採用清晰的分層架構，將核心功能、示範程式和測試分別獨立。

## 專案結構

```text
Day14.BogusExample/
├── src/
│   ├── BogusExample.Core/          # 核心類別庫
│   │   ├── Models/                 # 資料模型
│   │   │   ├── Product.cs          # 產品模型
│   │   │   ├── User.cs             # 使用者模型
│   │   │   ├── Order.cs            # 訂單模型
│   │   │   ├── Employee.cs         # 員工模型
│   │   │   └── TaiwanPerson.cs     # 台灣人員模型
│   │   ├── Extensions/             # 台灣本土化擴充
│   │   │   └── TaiwanDataExtensions.cs
│   │   └── Generators/             # 資料產生器
│   │       ├── BogusDataGenerator.cs
│   │       └── AutoBogusDataGenerator.cs
│   └── BogusExample.Demo/          # 示範程式 (Console App)
│       └── Program.cs              # 功能展示程式
├── tests/
│   └── BogusExample.Tests/         # 測試專案
│       ├── BasicTests/             # 基本功能測試
│       ├── ComparisonTests/        # Bogus vs AutoFixture 比較
│       ├── TaiwanTests/           # 台灣本土化測試
│       └── PerformanceTests/      # 效能測試
└── Day14.BogusExample.sln         # 解決方案檔案
```

## 專案架構設計

### 專案分工

| 專案                   | 類型         | 職責                                         |
| ---------------------- | ------------ | -------------------------------------------- |
| **BogusExample.Core**  | 類別庫       | 核心功能：Models、Extensions、Generators     |
| **BogusExample.Demo**  | Console 程式 | 示範程式：展示如何使用 Core 類別庫           |
| **BogusExample.Tests** | 測試專案     | 測試所有核心功能                             |

### 為什麼這樣設計？

- **可重用性** - Core 類別庫可以被其他專案引用
- **職責分離** - 每個專案有明確的職責
- **打包靈活** - Core 可以打包成 NuGet 套件
- **測試純淨** - 測試只針對類別庫，不包含 UI 邏輯
- **符合慣例** - 這是 .NET 專案的標準架構

## 主要功能

### 1. 基本資料產生

- **產品資料 (Product)**: 名稱、價格、分類、描述等
- **使用者資料 (User)**: 姓名、Email、年齡、角色等
- **訂單資料 (Order)**: 訂單號、客戶、項目、總金額等
- **員工資料 (Employee)**: 員工編號、職級、技能、專案經驗等

### 2. 台灣本土化資料擴充

- **台灣城市**: 直轄市和省轄市
- **台灣大學**: 知名大學和科技大學
- **台灣公司**: 知名企業和簡稱
- **身分證字號**: 符合台灣格式 (簡化版)
- **台灣手機號碼**: 09 開頭的手機號碼格式

### 3. Bogus vs AutoFixture 比較

- **效能比較**: 大量資料產生速度測試
- **資料真實性比較**: 語意化程度比較
- **客製化程度比較**: 自訂規則靈活性
- **記憶體使用比較**: 記憶體消耗分析

## 快速開始

### 1. 建置專案

```bash
dotnet build
```

### 2. 執行示範程式

```bash
dotnet run --project src/BogusExample.Demo
```

### 3. 執行測試

```bash
dotnet test
```

## 核心概念展示

### Bogus 的優勢

1. **語意化資料**: 產生有意義的測試資料
2. **本土化支援**: 支援多種語言和地區
3. **高效能**: 大量資料產生時效能優異
4. **靈活設定**: 支援複雜的自訂規則

### AutoFixture 的優勢

1. **自動化**: 最少的設定即可產生物件
2. **型別安全**: 強型別支援
3. **相依性注入**: 與測試框架整合良好
4. **擴充性**: 豐富的擴充套件

## 使用案例

### 1. 基本使用

```csharp
// 使用 Bogus 產生產品
var product = BogusDataGenerator.ProductFaker.Generate();

// 使用 AutoFixture 產生產品
var product = AutoBogusDataGenerator.CreateProductWithAutoFixture();
```

### 2. 台灣本土化資料

```csharp
// 產生台灣人資料
var person = BogusDataGenerator.TaiwanPersonFaker.Generate();
Console.WriteLine($"{person.Name} 住在 {person.City}，手機: {person.Mobile}");
```

### 3. 批量資料產生

```csharp
// 產生大量產品資料
var products = BogusDataGenerator.GenerateProducts(10000);
```

## 測試涵蓋範圍

- ✅ 基本功能測試 (8 個測試)
- ✅ 台灣本土化測試 (7 個測試)  
- ✅ Bogus vs AutoFixture 比較測試 (5 個測試)
- ✅ 效能比較測試 (5 個測試)
- ✅ 執行緒安全測試 (2 個測試)
- ✅ 大量資料產生測試 (1 個測試)

**總計: 28 個測試全部通過** ✅

## 相依套件

| 套件                                       | 版本   | 說明                                   |
| ------------------------------------------ | ------ | -------------------------------------- |
| **Bogus**                                  | 35.6.5 | 主要的假資料產生函式庫                 |
| **AutoFixture**                            | 4.18.1 | 測試資料產生框架                       |
| **AutoFixture.AutoNSubstitute**            | 4.18.1 | AutoFixture 的 NSubstitute 整合        |
| **NSubstitute**                            | 6.2.0  | 模擬框架                               |
| **xunit.v3.mtp-v2**                        | 3.2.2  | 測試框架（Microsoft.Testing.Platform） |
| **Microsoft.Testing.Extensions.TrxReport** | 2.3.3  | TRX 測試報告                           |
| **AwesomeAssertions**                      | 9.5.0  | 更好的斷言語法                         |
| **xunit.runner.visualstudio**              | 3.1.5  | IDE 測試總管（VSTest 探索）            |
| **Microsoft.NET.Test.Sdk**                 | 18.8.1 | IDE 測試總管相容                       |

> **關於 NU1608 警告**：NSubstitute 與全系列對齊升至 6.x，AutoFixture.AutoNSubstitute 4.18.1 宣告的相依上限是 NSubstitute < 6.0.0，因此 restore／build 會出現 NU1608 警告。這是預期行為，功能不受影響（28/28 測試通過），詳細說明見 Day13 文章「關於 NU1608 警告」一節。

## 效能比較結果

基於實際測試結果 (1000 個物件產生):

| 指標           | Bogus       | AutoFixture | 勝出            |
| -------------- | ----------- | ----------- | --------------- |
| **產生速度**   | ~1600ms     | ~400ms      | AutoFixture     |
| **記憶體使用** | ~5MB        | ~3.5MB      | AutoFixture     |
| **資料真實性** | 高 (語意化) | 低 (隨機)   | Bogus           |
| **客製化能力** | 高          | 中          | Bogus           |
| **本土化支援** | 豐富        | 基本        | Bogus           |

## 專案特色

### 台灣本土化支援

- 台灣城市 (12 個直轄市)
- 台灣大學 (14 所知名大學)  
- 台灣公司 (27 家知名企業)
- 台灣身分證格式
- 台灣手機號碼格式

### 完整測試涵蓋

- **單元測試**: 驗證所有核心功能
- **整合測試**: 驗證 Bogus 與 AutoFixture 整合
- **效能測試**: 大量資料產生效能測試
- **壓力測試**: 多執行緒安全測試

### 實用範例

- 電商訂單系統資料
- 員工管理系統資料
- 使用者註冊系統資料
- 產品庫存系統資料

## 學習重點

1. **理解 Bogus 基本概念**: Faker、規則定義、資料關聯
2. **掌握台灣本土化技巧**: 自訂擴展方法、文化特定資料
3. **效能最佳化策略**: 大量資料產生的最佳實務
4. **測試資料設計**: 如何設計有意義的測試資料
5. **工具選擇標準**: Bogus vs AutoFixture 的選擇依據

## 注意事項

1. **效能考量**:
   - AutoFixture 在簡單物件產生時效能較佳
   - Bogus 在複雜業務邏輯時更有優勢

2. **資料真實性**:
   - Bogus 提供語意化的真實資料
   - AutoFixture 提供型別安全的隨機資料

3. **客製化需求**:
   - Bogus 提供更靈活的客製化選項
   - AutoFixture 提供更簡潔的設定方式

4. **本土化需求**:
   - 使用自訂擴充方法支援台灣特定資料格式
   - 考慮文化差異和在地化需求

## 延伸學習

- [Bogus 官方文件](https://github.com/bchavez/Bogus)
- [AutoFixture 官方文件](https://github.com/AutoFixture/AutoFixture)
- [Day14.md](../../Day14.md) - 詳細教學文章
