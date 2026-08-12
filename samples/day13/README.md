# Day 13 - AutoFixture.AutoNSubstitute 整合範例

本專案展示如何整合 AutoFixture 與 NSubstitute，寫出更有效率的單元測試。

## 專案結構

```text
Day13.AutoNSubstitute/
├── src/
│   └── AutoNSubstitute.Core/          # 核心業務邏輯
│       ├── Entities/                  # 實體類別
│       ├── Dto/                       # 資料傳輸物件
│       ├── Repositories/              # 資料存取層介面
│       ├── Services/                  # 業務服務層
│       └── MapConfig/                 # Mapster 對應設定
└── tests/
    └── AutoNSubstitute.Tests/         # 測試專案
        ├── AutoFixtureConfigurations/ # AutoFixture 客製化設定
        ├── Attributes/                # 自訂屬性
        └── *.cs                       # 測試檔案
```

## 主要功能

### 1. AutoFixture.AutoNSubstitute 整合

透過 `AutoNSubstituteCustomization` 自動為介面建立 NSubstitute 的替身物件：

```csharp
[Theory]
[AutoDataWithCustomization]
public async Task IsExistsAsync_測試方法(
    [Frozen] IShipperRepository shipperRepository,
    ShipperService sut)
{
    // shipperRepository 會自動建立為 NSubstitute 的替身
    // sut 會自動注入所需的相依性
}
```

### 2. 自訂 AutoData 屬性

- `AutoDataWithCustomizationAttribute` - 整合 AutoNSubstitute 和 Mapster
- `InlineAutoDataWithCustomizationAttribute` - 支援固定值與自動產生的混合

### 3. Mapster 整合

透過 `MapsterMapperCustomization` 提供真實的對應器實作，而不是 Mock：

```csharp
public class MapsterMapperCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Register(() => this.Mapper);
    }
    // ... 真實的 Mapster 設定
}
```

### 4. CollectionSize 控制

使用 `CollectionSizeAttribute` 控制自動產生集合的大小：

```csharp
[Theory]
[AutoDataWithCustomization]
public async Task TestMethod(
    [CollectionSize(10)] IEnumerable<ShipperModel> models)
{
    // models 會包含正好 10 個元素
}
```

## 測試類別說明

### ShipperServiceBasicTests

展示基本的測試情境，包括參數驗證和基本功能測試。

### ShipperServiceAdvancedTests

展示進階功能，包括自動產生資料的使用和 CollectionSize 屬性。

### ShipperServiceParameterizedTests

展示參數化測試，使用 InlineAutoData 結合固定值與自動產生。

### ShipperServiceComplexDataTests

展示複雜的資料設定，包括使用 IFixture 參數進行精確控制。

### TraditionalVsAutoNSubstituteTests

對比傳統手動建立 Mock 與 AutoNSubstitute 自動建立的差異。

### AutoDataAttributeUsageTests

展示不同 AutoData 屬性的使用方式和效果。

## 核心概念

### 1. 工具型相依性 vs 業務相依性

- **工具型相依性**（如 IMapper）：使用真實實作，驗證對應邏輯
- **業務相依性**（如 IRepository）：使用 Mock，專注測試業務邏輯

### 2. Frozen 機制

`[Frozen]` 屬性確保相同類型的執行個體在測試中保持一致：

```csharp
[Theory]
[AutoDataWithCustomization]
public void Test([Frozen] IRepository repo1, [Frozen] IRepository repo2, Service sut)
{
    // repo1 和 repo2 是同一個執行個體
    // sut 注入的也是同一個執行個體
}
```

### 3. 自動相依性注入

AutoFixture.AutoNSubstitute 會：

- 偵測介面類型
- 自動建立 NSubstitute 替身
- 注入到建構函式中
- 保持執行個體一致性

## 執行測試

```bash
# 還原套件
dotnet restore

# 編譯專案
dotnet build

# 執行所有測試
dotnet test

# 執行特定測試類別
dotnet test --filter-class "AutoNSubstitute.Tests.ShipperServiceBasicTests"
```

## 套件相依性

### 核心專案

- Mapster - 物件對應
- Throw - 參數驗證

### 測試專案

- AutoFixture - 測試資料產生
- AutoFixture.AutoNSubstitute - NSubstitute 整合
- AutoFixture.Xunit3 - xUnit v3 整合
- NSubstitute - 模擬框架
- AwesomeAssertions - 強化的斷言
- xunit.v3.mtp-v2 - 測試框架（Microsoft.Testing.Platform）

> **關於 NU1608 警告**：本專案的 NSubstitute 與全系列對齊升至 6.x，但 AutoFixture.AutoNSubstitute 4.18.1 宣告的相依上限是 NSubstitute < 6.0.0，因此 restore／build 會出現 NU1608 警告。這是預期行為，功能不受影響（73/73 測試通過），刻意不用 NoWarn 壓掉；待 AutoFixture 5.0 穩定版發佈後升級即可消除。詳細說明見文章「關於 NU1608 警告」一節。

## 學習重點

1. **減少樣板程式碼**：自動建立相依性，專注於測試邏輯
2. **提升維護性**：建構函式變更時測試不需同步修改
3. **保持測試意圖**：清楚表達測試目的
4. **適度使用**：在簡化與可讀性間取得平衡

## 相關文章

這個範例專案對應到「30天測試修練」系列的第13天文章：
**Day 13 – NSubstitute 與 AutoFixture 的整合應用**
