---
day: 13
title: "Day 13 - NSubstitute 與 AutoFixture 的整合應用"
sample: samples/day13
target_framework: net10.0
packages:
  - AutoFixture
  - AutoFixture.AutoNSubstitute
  - AutoFixture.Xunit3
  - AwesomeAssertions
  - Mapster
  - Mapster.Core
  - Microsoft.Testing.Extensions.TrxReport
  - NSubstitute
  - Throw
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 13 - NSubstitute 與 AutoFixture 的整合應用

## 前言

前面學會了 NSubstitute 的相依模擬和 AutoFixture 的資料產生。但實際開發時，當服務類別有多個相依性，手動建立每個 Mock 物件會讓測試程式碼變得冗長。AutoFixture.AutoData 提供了更簡潔的解決方案，可以自動處理相依性注入並產生測試資料。

這篇來看看如何結合 AutoFixture.AutoData 與 NSubstitute，讓測試寫得更有效率。

## AutoFixture.AutoNSubstitute 套件介紹

### 套件概述

AutoFixture.AutoNSubstitute 是 AutoFixture 生態系統中的一個擴充套件，專門用來整合 NSubstitute 模擬框架。它提供了自動模擬（Auto-Mocking）功能，能夠自動為介面和抽象類別建立 NSubstitute 的替身物件。

> **NuGet Package**: AutoFixture.AutoNSubstitute  
> **套件連結**：[https://www.nuget.org/packages/AutoFixture.AutoNSubstitute/](https://www.nuget.org/packages/AutoFixture.AutoNSubstitute/)  
> **官方文件**：[https://github.com/autofixture/autofixture#mocking-libraries](https://github.com/autofixture/autofixture#mocking-libraries)

安裝 NuGet Package：

```bash
dotnet add package AutoFixture.AutoNSubstitute
```

### 關於 NU1608 警告：與 NSubstitute 6 搭配使用

本系列的範例專案把 NSubstitute 統一升到 6.2.0（Day 7 起的各篇都用這個版本）。但 AutoFixture.AutoNSubstitute 目前的穩定版 4.18.1 在套件中宣告的相依範圍是 `NSubstitute (>= 2.0.3 && < 6.0.0)`，兩者放在同一個專案時，restore 會出現這個警告：

```text
warning NU1608: Detected package version outside of dependency constraint:
AutoFixture.AutoNSubstitute 4.18.1 requires NSubstitute (>= 2.0.3 && < 6.0.0)
but version NSubstitute 6.2.0 was resolved.
```

這個警告的意思是：實際解析出來的 NSubstitute 版本超出了 AutoFixture.AutoNSubstitute 宣告的上限。它不是錯誤，專案照常編譯，本篇的 73 個測試在 NSubstitute 6.2.0 下也全數通過——AutoNSubstituteCustomization 用到的只是 `Substitute.For<T>()` 這類穩定 API，NSubstitute 6 沒有動到它們。

處理方式我選擇**保留警告、不壓掉它**，理由有三個：

1. **版本一致比較重要**。前面幾篇已經用 NSubstitute 6.2.0，如果這幾篇為了消警告退回 5.3.0，同一個系列出現兩種版本，反而讓人疑惑。
2. **這個警告說的是真話**——AutoFixture 4.x 的相依上限確實沒涵蓋 NSubstitute 6。用 `NoWarn` 把它藏起來，讀者在自己的專案裡遇到時反而少了判斷依據。
3. **它會自然消失**。AutoFixture 5.0 正在 rc 階段，等穩定版發佈、相依上限更新後，這個警告就沒了；到時候範例專案升級即可。

如果你的專案還在用 NSubstitute 5.x，就完全沒有這個問題；要不要跟著升 6.x，可以等到 AutoFixture 5.0 穩定版一起評估。

### AutoNSubstituteCustomization 的作用

當我們在 AutoFixture 中加入 `AutoNSubstituteCustomization` 時，它會自動：

1. **偵測介面類型**：當 AutoFixture 遇到介面或抽象類別時
2. **自動建立替身**：使用 NSubstitute 的 `Substitute.For<T>()` 建立 Mock 物件
3. **注入相依性**：將這些替身物件注入到需要的建構函式中
4. **保持執行個體一致性**：確保相同類型的替身在同一個測試中保持一致

### 基本使用範例

```csharp
using AutoFixture;
using AutoFixture.AutoNSubstitute;

// 建立包含 AutoNSubstitute 功能的 Fixture
var fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

// 自動建立服務和其相依性
var service = fixture.Create<MyService>();

// MyService 的所有介面相依性都會自動變成 NSubstitute 的替身
```

### 傳統方式 vs AutoNSubstitute 方式

**傳統手動方式**：

```csharp
[Fact]
public void TraditionalWay()
{
    // Arrange - 手動建立每個相依性
    var repository = Substitute.For<IRepository>();
    var logger = Substitute.For<ILogger>();
    var notificationService = Substitute.For<INotificationService>();
    var sut = new OrderService(repository, logger, notificationService);

    // 設定替身行為
    repository.GetOrder(Arg.Any<int>()).Returns(someOrder);
    
    // Act & Assert...
}
```

**使用 AutoNSubstitute**：

```csharp
[Theory]
[AutoDataWithCustomization]
public void WithAutoNSubstitute([Frozen] IRepository repository, OrderService sut)
{
    // Arrange - 相依性已自動建立，只需設定需要的行為
    repository.GetOrder(Arg.Any<int>()).Returns(someOrder);
    
    // Act & Assert...
}
```

這個範例有兩個關鍵，缺一就無法達成上面宣稱的效果：

1. **必須用含 `AutoNSubstituteCustomization` 的自訂 AutoData 屬性**（這裡是 `[AutoDataWithCustomization]`，稍後會實作）。原生的 `[AutoData]` 用的是未經客製化的 Fixture，遇到 `IRepository` 這種介面相依性會直接丟出無法建立執行個體的例外。
2. **`[Frozen]` 的相依性參數要放在 SUT 之前**。AutoFixture 是依參數順序逐一解析的：先凍結 `repository`，之後建立 `OrderService sut` 時才會注入同一個被凍結的替身。如果把 `sut` 寫在前面，SUT 會先用另一個執行個體建構，`[Frozen]` 之後才生效，你在測試裡設定的行為就不會作用在 SUT 實際持有的相依性上。

### 解決的核心問題

AutoFixture.AutoNSubstitute 主要解決以下問題：

1. **減少樣板程式碼**：不需要手動建立每個介面的替身
2. **簡化複雜相依性**：自動處理多層相依性的建立
3. **提升測試維護性**：當建構函式變更時，測試程式碼不需要同步修改
4. **保持測試重點**：讓開發者專注於測試邏輯而非物件建立

## AutoFixture.AutoData 的核心概念

### FrozenAttribute 的作用

在 AutoFixture.Xunit 中，`[Frozen]` 屬性用來控制測試中某個類型的執行個體。當參數被標註為 `[Frozen]` 時，AutoFixture 會建立這個類別的一個執行個體並凍結它，後續在測試方法中都會使用同一個已凍結的執行個體。

這個機制特別適合有許多相依注入的測試目標類別，可以保證測試的穩定性和一致性。

### 準備自訂 AutoData 屬性

先建立自訂 AutoData 屬性，接上 AutoNSubstitute。

## 實作範例：ShipperService 測試

### 專案結構說明

在開始之前，需要注意本範例使用的資料模型位於：

- **AutoNSubstitute.Core.Entities.ShipperModel**：主要的領域實體，用於業務邏輯處理
- **AutoNSubstitute.Core.Dto.ShipperDto**：資料傳輸物件，用於與外部系統交換資料

請確保在 `using` 陳述式中正確引用 `AutoNSubstitute.Core.Entities` 命名空間，避免與其他同名類別混淆。

### 目標類別結構

```csharp
using AutoNSubstitute.Core.Dto;
using AutoNSubstitute.Core.Entities;
using AutoNSubstitute.Core.Misc;
using AutoNSubstitute.Core.Repositories;
using AutoNSubstitute.Core.Validation;
using MapsterMapper;
using Throw;

namespace AutoNSubstitute.Core.Services;

/// <summary>
/// 出貨商服務實作
/// </summary>
public class ShipperService : IShipperService
{
    private readonly IMapper _mapper;
    private readonly IShipperRepository _shipperRepository;

    /// <summary>
    /// 建構函式
    /// </summary>
    /// <param name="mapper">對應器</param>
    /// <param name="shipperRepository">出貨商資料庫</param>
    public ShipperService(IMapper mapper, IShipperRepository shipperRepository)
    {
        this._mapper = mapper;
        this._shipperRepository = shipperRepository;
    }

    /// <summary>
    /// 以 ShipperId 查詢資料是否存在
    /// </summary>
    /// <param name="shipperId">出貨商編號</param>
    /// <returns>是否存在</returns>
    public async Task<bool> IsExistsAsync(int shipperId)
    {
        shipperId.Throw().IfLessThanOrEqualTo(0);
        var exists = await this._shipperRepository.IsExistsAsync(shipperId);
        return exists;
    }

    /// <summary>
    /// 以 ShipperId 查詢出貨商資料
    /// </summary>
    /// <param name="shipperId">出貨商編號</param>
    /// <returns>出貨商資料；查無資料時回傳 null</returns>
    public async Task<ShipperDto?> GetAsync(int shipperId)
    {
        shipperId.Throw().IfLessThanOrEqualTo(0);

        var exists = await this._shipperRepository.IsExistsAsync(shipperId);
        if (!exists)
        {
            return null;
        }

        var model = await this._shipperRepository.GetAsync(shipperId);
        var shipper = this._mapper.Map<ShipperModel, ShipperDto>(model);
        return shipper;
    }

    /// <summary>
    /// 搜尋出貨商資料
    /// </summary>
    /// <param name="companyName">公司名稱</param>
    /// <param name="phone">電話號碼</param>
    /// <returns>符合條件的出貨商資料</returns>
    public async Task<IEnumerable<ShipperDto>> SearchAsync(string companyName, string phone)
    {
        if (string.IsNullOrWhiteSpace(companyName) && string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("companyName 與 phone 不可都為空白");
        }

        var totalCount = await this.GetTotalCountAsync();
        if (totalCount.Equals(0))
        {
            return [];
        }

        var models = await this._shipperRepository.SearchAsync(companyName ?? string.Empty, phone ?? string.Empty);
        var shippers = this._mapper.Map<IEnumerable<ShipperDto>>(models);
        return shippers;
    }

    /// <summary>
    /// 新增
    /// </summary>
    /// <param name="shipper">出貨商資料</param>
    /// <returns>執行結果</returns>
    public async Task<IResult> CreateAsync(ShipperDto shipper)
    {
        ModelValidator.Validate(shipper, nameof(shipper));

        var model = this._mapper.Map<ShipperDto, ShipperModel>(shipper);
        var result = await this._shipperRepository.CreateAsync(model);
        return result;
    }

    // 其他方法實作...
}
```

### 設定 Mapster 客製化

因為測試目標使用 Mapster 而非 AutoMapper，我們需要建立對應的客製化。

**為什麼不讓 AutoNSubstitute 自動處理？**

雖然 AutoNSubstitute 可以自動為 `IMapper` 介面建立替身物件，但這並不是我們想要的結果：

1. **IMapper 是工具型相依性**：它負責物件對應，不承載業務邏輯
2. **需要真實的對應設定**：測試中需要驗證對應邏輯是否正確，使用 Mock 會失去這個驗證能力
3. **設定複雜度**：如果使用 Mock，需要為每個對應方法設定 Returns，反而增加測試複雜度
4. **測試意圖**：我們要測試的是業務邏輯，不是 IMapper 本身的行為

因此，我們選擇建立真實的 Mapster 設定，讓 AutoFixture 注入已設定好的 IMapper 執行個體：

```csharp
using AutoFixture;
using AutoNSubstitute.Core.MapConfig;
using Mapster;
using MapsterMapper;

namespace AutoNSubstitute.Tests.AutoFixtureConfigurations;

/// <summary>
/// Mapster 對應器客製化
/// </summary>
public class MapsterMapperCustomization : ICustomization
{
    /// <summary>
    /// 客製化 Fixture
    /// </summary>
    /// <param name="fixture">Fixture 執行個體</param>
    public void Customize(IFixture fixture)
    {
        fixture.Register(() => this.Mapper);
    }

    private IMapper? _mapper;

    private IMapper Mapper
    {
        get
        {
            if (this._mapper is not null)
            {
                return this._mapper;
            }

            var typeAdapterConfig = new TypeAdapterConfig();
            typeAdapterConfig.Scan(typeof(ServiceMapRegister).Assembly);
            this._mapper = new Mapper(typeAdapterConfig);
            return this._mapper;
        }
    }
}
```

### 建立自訂 AutoData 屬性

為了在測試中同時使用 AutoNSubstitute 的自動模擬功能和 Mapster 的真實對應器，我們需要建立自訂的 AutoData 屬性。

**AutoDataWithCustomizationAttribute 的設計目的**：

1. **整合多種客製化**：將 AutoNSubstituteCustomization 和 MapsterMapperCustomization 組合在一起
2. **簡化測試設定**：避免在每個測試方法中重複設定 Fixture
3. **標準化測試模式**：為整個專案提供一致的測試基礎設施
4. **封裝複雜性**：將 Fixture 的複雜設定隱藏在屬性內部

**CreateFixture 方法的處理行為**：

- **建立基礎 Fixture**：使用 `new Fixture()` 建立基本的 AutoFixture 執行個體
- **加入 AutoNSubstitute 支援**：呼叫 `.Customize(new AutoNSubstituteCustomization())` 啟用自動模擬
- **加入 Mapster 支援**：呼叫 `.Customize(new MapsterMapperCustomization())` 注入真實的對應器設定
- **鏈式呼叫**：使用 Fluent API 讓多個客製化設定可以連續套用

```csharp
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit3;

namespace AutoNSubstitute.Tests.AutoFixtureConfigurations;

/// <summary>
/// 包含客製化設定的 AutoData 屬性
/// </summary>
public class AutoDataWithCustomizationAttribute : AutoDataAttribute
{
    /// <summary>
    /// 建構函式
    /// </summary>
    public AutoDataWithCustomizationAttribute() : base(CreateFixture)
    {
    }

    internal static IFixture CreateFixture()
    {
        var fixture = new Fixture().Customize(new AutoNSubstituteCustomization())
                                   .Customize(new MapsterMapperCustomization());

        return fixture;
    }
}
```

### 建立 InlineAutoData 版本

在某些測試情境中，我們需要同時使用固定的測試值（如邊界值、特殊值）和自動產生的物件。這時候就需要 InlineAutoData 的功能。

**InlineAutoDataWithCustomizationAttribute 的設計目的**：

1. **混合測試資料策略**：結合預定義的固定值與 AutoFixture 產生的動態資料
2. **參數化測試支援**：特別適用於需要測試多組邊界值或特殊情況的情境
3. **保持客製化設定**：維持與 AutoDataWithCustomizationAttribute 相同的 Fixture 設定
4. **提升測試涵蓋率**：固定值守住關鍵案例，自動產生的資料則擴展測試範圍

**與純 AutoData 的差異**：

- **AutoData**：所有參數都由 AutoFixture 自動產生
- **InlineAutoData**：前幾個參數使用固定值，其餘參數由 AutoFixture 產生
- **應用情境**：邊界值測試、例外參數測試、多組固定條件測試

**CreateFixture 方法的處理行為**：

- **重用相同的設定邏輯**：與 AutoDataWithCustomizationAttribute 共用同一個 `CreateFixture` 方法（宣告為 `internal static`）
- **確保一致性**：保證兩種屬性在相依性處理上的行為完全一致
- **簡化維護**：當需要修改 Fixture 設定時，只需要在一個地方進行變更

```csharp
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit3;

namespace AutoNSubstitute.Tests.AutoFixtureConfigurations;

/// <summary>
/// 包含客製化設定的 InlineAutoData 屬性
/// </summary>
public class InlineAutoDataWithCustomizationAttribute : InlineAutoDataAttribute
{
    /// <summary>
    /// 建構函式
    /// </summary>
    /// <param name="values">固定值</param>
    public InlineAutoDataWithCustomizationAttribute(params object[] values)
        : base(AutoDataWithCustomizationAttribute.CreateFixture, values)
    {
    }
}
```

### 實作重點說明

**為什麼是 `base(AutoDataWithCustomizationAttribute.CreateFixture, values)`？**

1. **建構式簽章在 Xunit3 改變了**：

   ```csharp
   // AutoFixture.Xunit2（舊）
   public InlineAutoDataAttribute(AutoDataAttribute autoDataAttribute, params object[] values)

   // AutoFixture.Xunit3（新）
   public InlineAutoDataAttribute(Func<IFixture> fixtureFactory, params object[] values)
   ```

   Xunit3 不再提供接收 `AutoDataAttribute` 執行個體的多載，改為直接接收 fixture factory 委派。

2. **遷移陷阱——編譯會過、執行才炸**：

   ```csharp
   // X 錯誤：v2 的寫法在 v3 下仍能編譯，但行為完全不對
   public InlineAutoDataWithCustomizationAttribute(params object[] values)
       : base(new AutoDataWithCustomizationAttribute(), values)
   ```

   C# 會把整組引數綁到 `InlineAutoDataAttribute(params object[] values)` 這個多載：attribute 執行個體被當成第一個 inline 值塞進測試參數（執行期錯誤訊息類似 `Object of type 'AutoDataWithCustomizationAttribute' cannot be converted to type 'System.Int32'`），客製化設定也完全沒掛上，介面參數會因為少了 AutoNSubstituteCustomization 而拋出 `ObjectCreationException`。

3. **正確的寫法**：

   ```csharp
   // O 正確：把 fixture factory 直接傳給 base
   public InlineAutoDataWithCustomizationAttribute(params object[] values)
       : base(AutoDataWithCustomizationAttribute.CreateFixture, values)
   ```

   `CreateFixture` 宣告為 `internal static`，讓兩個屬性共用同一份設定。

4. **重用現有邏輯的優勢**：
   - 不需要重複實作 `CreateFixture` 方法
   - 確保與 `AutoDataWithCustomizationAttribute` 的行為完全一致
   - 當 Fixture 設定變更時，只需要在一個地方修改

## 測試實作範例

ShipperServiceBasicTests.cs

### 基本測試：無需設定相依行為

```csharp
[Theory]
[AutoDataWithCustomization]
public async Task IsExistsAsync_輸入的ShipperId為0時_應拋出ArgumentOutOfRangeException(ShipperService sut)
{
    // Arrange
    var shipperId = 0;

    // Act
    var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.IsExistsAsync(shipperId));

    // Assert
    exception.Message.Should().Contain(nameof(shipperId));
}
```

在這個測試中，`sut`（System Under Test）會自動由 AutoFixture 建立，包含所有必要的相依性。

### 進階測試：設定相依行為

```csharp
[Theory]
[AutoDataWithCustomization]
public async Task IsExistsAsync_輸入的ShipperId_資料不存在_應回傳false(
    [Frozen] IShipperRepository shipperRepository,
    ShipperService sut)
{
    // Arrange
    var shipperId = 99;
    
    shipperRepository.IsExistsAsync(Arg.Any<int>()).Returns(false);

    // Act
    var actual = await sut.IsExistsAsync(shipperId);

    // Assert
    actual.Should().BeFalse();
}
```

`[Frozen]` 屬性可以固定相依性的 Stub，方便設定其行為。

### 使用自動產生的資料

```csharp
[Theory]
[AutoDataWithCustomization]
public async Task GetAsync_輸入的ShipperId_資料有存在_應回傳model(
    [Frozen] IShipperRepository shipperRepository,
    ShipperService sut,
    ShipperModel model)
{
    // Arrange
    var shipperId = model.ShipperId;

    shipperRepository.IsExistsAsync(Arg.Any<int>()).Returns(true);
    shipperRepository.GetAsync(Arg.Any<int>()).Returns(model);

    // Act
    var actual = await sut.GetAsync(shipperId);

    // Assert
    actual.Should().NotBeNull();
    actual.ShipperId.Should().Be(shipperId);
    actual.CompanyName.Should().Be(model.CompanyName);
    actual.ContactName.Should().Be(model.ContactName);
}
```

這裡的 `model` 也是由 AutoFixture 自動產生，包含合理的測試資料。

### 參數化測試

```csharp
[Theory]
[InlineAutoDataWithCustomization(0, 10, nameof(from))]
[InlineAutoDataWithCustomization(-1, 10, nameof(from))]
[InlineAutoDataWithCustomization(1, 0, nameof(size))]
[InlineAutoDataWithCustomization(1, -1, nameof(size))]
public async Task GetCollectionAsync_from與size輸入不合規格內容_應拋出ArgumentOutOfRangeException(
    int from, int size, string parameterName, ShipperService sut)
{
    // Act
    var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
        () => sut.GetCollectionAsync(from, size));

    // Assert
    exception.Message.Should().Contain(parameterName);
}
```

結合固定的測試數值與自動產生的 SUT。

### 使用 CollectionSize 控制集合大小

```csharp
[Theory]
[AutoDataWithCustomization]
public async Task GetAllAsync_資料表裡有10筆資料_回傳的集合裡有10筆(
    [Frozen] IShipperRepository shipperRepository,
    ShipperService sut,
    [CollectionSize(10)] IEnumerable<ShipperModel> models)
{
    // Arrange
    shipperRepository.GetAllAsync().Returns(models);

    // Act
    var actual = await sut.GetAllAsync();

    // Assert
    actual.Should().NotBeEmpty();
    actual.Should().HaveCount(10);
}
```

### 複雜的資料設定

```csharp
[Theory]
[AutoDataWithCustomization]
public async Task SearchAsync_companyName輸入資料_phone無輸入_有符合條件的資料_回傳集合應包含符合條件的資料(
    IFixture fixture,
    [Frozen] IShipperRepository shipperRepository,
    ShipperService sut)
{
    // Arrange
    var models = fixture.Build<ShipperModel>()
                        .With(x => x.CompanyName, "test")
                        .CreateMany(1);

    shipperRepository.GetTotalCountAsync().Returns(1);
    shipperRepository.SearchAsync(Arg.Any<string>(), Arg.Any<string>())
                     .Returns(models);

    const string companyName = "test";
    const string phone = "";

    // Act
    var actual = await sut.SearchAsync(companyName, phone);

    // Assert
    actual.Should().NotBeEmpty();
    actual.Should().HaveCount(1);
    actual.Any(x => x.CompanyName == companyName).Should().BeTrue();
}
```

### 驗證參數的測試案例

由於 `SearchAsync` 方法包含參數驗證邏輯，我們也需要測試這些驗證規則：

```csharp
[Theory]
[InlineAutoDataWithCustomization(null!, null!)]
[InlineAutoDataWithCustomization("", null!)]
[InlineAutoDataWithCustomization(null!, "")]
[InlineAutoDataWithCustomization(null!, null!)]
public async Task SearchAsync_companyName與phone輸入不合規格的內容_應拋出ArgumentException(
    string? companyName, string? phone, ShipperService sut)
{
    // arrange
    const string exceptionMessage = "companyName 與 phone 不可都為空白";

    // act
    var exception = await Assert.ThrowsAsync<ArgumentException>(
        () => sut.SearchAsync(companyName!, phone!));

    // assert
    exception.Message.Should().Be(exceptionMessage);
}
```

**Nullable 參考類型處理說明**：

1. **參數宣告使用 `string?`**：因為測試需要傳入 `null` 值來測試參數驗證
2. **InlineAutoData 中使用 `null!`**：告訴編譯器這裡的 `null` 是有意為之的測試資料
3. **方法呼叫使用 `!` 運算子**：在測試方法中使用 null-forgiving 運算子，因為我們確定要測試 `null` 參數的處理邏輯

這樣的處理方式既能滿足 Nullable 參考類型的編譯檢查，又能正確測試參數驗證邏輯。

這個範例用 `IFixture` 參數精確控制測試資料的產生。

## 整合的優勢與實務考量

### 核心優勢

1. **大幅減少樣板程式碼**：不需要手動建立每個相依性的 Mock
2. **自動處理複雜相依圖**：AutoFixture 會自動解析並建立所需的物件
3. **測試資料自動產生**：減少寫死在程式裡的測試資料
4. **保持測試意圖清晰**：專注於測試邏輯而非物件建立
5. **提升開發效率**：特別適合有多個相依性的複雜服務類別

### 注意事項與限制

1. **學習成本**：需要理解 AutoFixture 和 Frozen 的運作機制
2. **除錯複雜度**：自動產生的物件可能讓除錯變得困難
3. **測試可讀性**：過度使用可能讓測試意圖不明確
4. **效能考量**：物件建立的開銷可能影響測試執行速度

### 適用情境判斷

**建議使用的情境**：

- 服務層測試，特別是有多個相依性的類別
- 需要大量測試資料的參數化測試
- 複雜業務邏輯的驗證

**謹慎使用的情境**：

- 簡單的單一相依性測試（手動建立可能更清晰）
- 需要精確控制每個物件屬性的測試
- 團隊成員對 AutoFixture 不熟悉的專案

## 實務導入建議

### 導入策略

1. **漸進式採用**：從簡單的服務類別開始，逐步擴展到複雜情境
2. **團隊培訓**：確保團隊成員理解 AutoFixture 和 Frozen 的概念
3. **建立規範**：制定何時使用自動產生、何時手動建立的準則
4. **效能監控**：注意測試執行時間，避免過度複雜的物件圖

### 最佳實務

1. **明確的測試意圖**：即使使用自動產生，測試名稱和斷言仍要清楚表達意圖
2. **適度的控制**：需要時用 `IFixture` 參數精確控制資料產生
3. **合理的抽象**：建立可重用的 Customization 和 AutoData 屬性
4. **文件化設定**：記錄自訂 AutoData 屬性的用途和設定

## 今日小結

本篇整合 NSubstitute 與 AutoFixture，讓 AutoFixture.AutoNSubstitute 自動建立並注入測試替身。

### 關鍵技術要點

1. **AutoNSubstituteCustomization**：自動為介面建立 NSubstitute 替身
2. **自訂 AutoData 屬性**：整合多種客製化設定，簡化測試程式碼
3. **FrozenAttribute 機制**：確保相同類型的執行個體在測試中保持一致
4. **混合測試策略**：用 InlineAutoData 結合固定值與自動產生

### 實務價值

這種整合方式有幾個直接效果：

- **簡化複雜服務的測試設定**：自動處理多個相依性的建立與注入
- **提升測試維護性**：減少重複的物件建立程式碼
- **保持測試重點**：讓開發者專注於測試邏輯本身
- **建立標準化模式**：用自訂屬性統一專案的測試設定

### 學習行程回顧

Day 7 介紹 NSubstitute，Day 10～12 則逐步加入 AutoFixture。把兩者接起來後，可以少寫一大段 Arrange 程式碼，同時保留測試需要的明確控制。

是否採用這套組合，仍要看專案需求、團隊熟悉度與維護成本。若自訂屬性反而讓資料來源難以辨識，就應退回較明確的測試設定。

## 相關參考資料

- [使用 AutoFixture.AutoData 來改寫以前的測試程式碼｜mrkt 的程式學習筆記](https://www.dotblogs.com.tw/mrkt/2024/09/29/191300)
- [AutoFixture.AutoNSubstitute NuGet Package](https://www.nuget.org/packages/AutoFixture.AutoNSubstitute/)
- [AutoFixture Documentation - Auto Mocking](https://autofixture.readthedocs.io/en/stable/AutoMoqAutomocking/)
- [NSubstitute Documentation](https://nsubstitute.github.io/help/getting-started/)

明天我們將學習另一個測試資料產生工具 Bogus，探討它與 AutoFixture 的差異，以及在不同情境下的選擇策略。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day13>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第十三天。明天會介紹 Day 14 - Bogus 入門：與 AutoFixture 的差異比較。**
