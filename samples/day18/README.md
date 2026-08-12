# Day 18 - FluentValidation Test Extensions 範例專案

本專案示範如何使用 FluentValidation Test Extensions 寫出強健的業務規則驗證測試。

## 專案結構

```text
Day18.FluentValidationTesting/
├── Day18.FluentValidationTesting.sln
├── src/
│   └── ValidationExample.Core/
│       ├── Models/
│       │   └── UserRegistrationRequest.cs          # 使用者註冊請求模型
│       ├── Validators/
│       │   ├── UserRegistrationValidator.cs        # 基本驗證器
│       │   └── UserRegistrationAsyncValidator.cs   # 非同步驗證器
│       ├── Services/
│       │   └── IUserService.cs                     # 使用者服務介面
│       └── ValidationExample.Core.csproj
└── tests/
    └── ValidationExample.Tests/
        ├── Extensions/
        │   └── FakeTimeProviderExtensions.cs       # TimeProvider 擴充方法
        ├── Validators/
        │   ├── UserRegistrationValidatorTests.cs   # FluentValidation Test Extensions 測試
        │   ├── UserRegistrationAsyncValidatorTests.cs # 非同步驗證測試
        │   ├── ConditionalValidationTests.cs       # 條件式驗證測試
        │   ├── RoleValidationTests.cs              # 角色驗證測試
        │   └── FakeTimeProviderExtensionDemoTests.cs # TimeProvider 擴充方法展示
        ├── GlobalUsings.cs
        └── ValidationExample.Tests.csproj
```

## 主要特色

### 1. 完整的驗證規則覆蓋

- **使用者名稱**：非空、長度限制、格式驗證
- **電子郵件**：非空、格式驗證、長度限制
- **密碼**：複雜度要求、長度限制
- **確認密碼**：與密碼一致性驗證
- **年齡與生日**：一致性驗證、年齡限制
- **電話號碼**：條件式驗證（可選但有格式要求）
- **角色**：有效性驗證、非空驗證
- **條款同意**：必須為 true

### 2. 測試技術展示

- **FluentValidation Test Extensions**：`ShouldHaveValidationErrorFor` / `ShouldNotHaveValidationErrorFor`
- **參數化測試**：`Theory` 和 `InlineData` 大量測試案例
- **時間控制測試**：使用 `Microsoft.Extensions.TimeProvider.Testing` 的 `FakeTimeProvider` 控制時間相關驗證
- **AwesomeAssertions**：遵循 Instructions 要求的斷言庫
- **TimeProvider 注入**：驗證器接受 `TimeProvider` 參數以支援可測試的時間邏輯

## 快速開始

### 1. 還原套件

```bash
dotnet restore
```

### 2. 建置專案

```bash
dotnet build
```

### 3. 執行測試

```bash
# 執行所有測試
dotnet test

# 執行特定測試類別
dotnet test --filter-class "ValidationExample.Tests.Validators.UserRegistrationValidatorTests"
dotnet test --filter-class "ValidationExample.Tests.Validators.RoleValidationTests"
```

## 主要測試案例

### 基本驗證測試

```csharp
[Fact]
public void Validate_有效的使用者名稱_應該通過驗證()
{
    // Arrange
    var request = UserRegistrationRequestMother.Valid();
    
    // Act
    var result = _validator.TestValidate(request);
    
    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Username);
}
```

### 參數化測試

```csharp
[Theory]
[InlineData("", "使用者名稱不可為 null 或空白")]
[InlineData("a", "使用者名稱長度必須在 3 到 20 個字元之間")]
public void Validate_無效的使用者名稱_應該回傳對應錯誤訊息(string username, string expectedError)
{
    // 測試實作
}
```

### 非同步驗證測試

```csharp
[Fact]
public async Task ValidateAsync_使用者名稱可用_應該通過驗證()
{
    // Arrange
    _mockUserService.IsUsernameAvailableAsync("newuser")
                   .Returns(Task.FromResult(true));
    
    // Act
    var result = await _validator.TestValidateAsync(request);
    
    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Username);
    await _mockUserService.Received(1).IsUsernameAvailableAsync("newuser");
}
```

### 時間控制測試

```csharp
[Fact]
public void Validate_使用FakeTimeProvider_應該正確計算年齡()
{
    // Arrange
    _fakeTimeProvider.SetUtcNow(new DateTime(2024, 6, 15));
    
    // Act & Assert
    // 測試年齡與生日的一致性
}
```

## 套件依賴

### 主要專案 (ValidationExample.Core)

- `FluentValidation` (12.1.1)

### 測試專案 (ValidationExample.Tests)

- `xunit.v3.mtp-v2` (3.2.2)（Microsoft.Testing.Platform 模式）
- `Microsoft.Testing.Extensions.TrxReport` (2.3.3)
- `FluentValidation` (12.1.1)（`FluentValidation.TestHelper` 命名空間隨主套件提供）
- `AwesomeAssertions` (9.5.0)
- `Microsoft.Extensions.TimeProvider.Testing` (10.9.0)
- `NSubstitute` (6.2.0)

## 最佳實務展示

### 1. TimeProvider 注入與測試

驗證器透過建構子接受 `TimeProvider`，支援時間相關的測試邏輯：

```csharp
public class UserRegistrationValidator : AbstractValidator<UserRegistrationRequest>
{
    private readonly TimeProvider _timeProvider;

    public UserRegistrationValidator() : this(TimeProvider.System)
    {
    }

    public UserRegistrationValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        SetupValidationRules();
    }
}
```

### 2. FakeTimeProvider 控制時間

在測試中使用 `FakeTimeProvider` 來控制時間相關的驗證：

```csharp
// 傳統方式：使用 SetUtcNow
_fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

// 推薦方式：使用 SetLocalNow 擴充方法（更直觀）
_fakeTimeProvider.SetLocalNow(new DateTime(2024, 1, 1, 0, 0, 0));
```

### 3. FakeTimeProviderExtensions 擴充方法

專案中包含 `SetLocalNow` 擴充方法，讓時間設定更加直觀：

```csharp
public static class FakeTimeProviderExtensions
{
    /// <summary>
    /// 設定 FakeTimeProvider 的本地時間
    /// </summary>
    /// <param name="fakeTimeProvider">FakeTimeProvider 實例</param>
    /// <param name="localDateTime">要設定的本地時間</param>
    public static void SetLocalNow(this FakeTimeProvider fakeTimeProvider, DateTime localDateTime)
    {
        fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
        var utcTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZoneInfo.Local);
        fakeTimeProvider.SetUtcNow(utcTime);
    }
}
```

#### 使用範例

```csharp
[Fact]
public void 展示SetLocalNow擴充方法的使用()
{
    // Arrange
    var fakeTimeProvider = new FakeTimeProvider();
    fakeTimeProvider.SetLocalNow(new DateTime(2024, 3, 15, 14, 0, 0)); // 下午 2 點
    
    var validator = new UserRegistrationValidator(fakeTimeProvider);
    // 其他測試邏輯...
}
```

### 4. 完整的測試範例

```csharp
public class UserRegistrationValidatorTests
{
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly UserRegistrationValidator _validator;

    public UserRegistrationValidatorTests()
    {
        // 設定固定的測試時間
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _validator = new UserRegistrationValidator(_fakeTimeProvider);
    }

    [Fact]
    public void Validate_年齡與出生日期不一致_應該驗證失敗()
    {
        // Arrange
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 5, 12, 0, 0, 0, TimeSpan.Zero));
        
        var request = CreateValidRequest();
        request.BirthDate = new DateTime(2000, 1, 1); // 應該是 24 歲
        request.Age = 29; // 但設定為 29 歲，不一致

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BirthDate)
              .WithErrorMessage("生日與年齡不一致");
    }
}
```

### 5. FluentValidation Test Extensions 的核心功能

使用 `TestValidate()` 方法和相關的斷言方法來測試驗證邏輯：

```csharp
[Fact]
public void Validate_有效的使用者名稱_應該通過驗證()
{
    // Arrange
    var request = CreateValidRequest();
    request.Username = "validuser123";

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Username);
}

[Theory]
[InlineData("", "使用者名稱不可為 null 或空白")]
[InlineData("a", "使用者名稱長度必須在 3 到 20 個字元之間")]
[InlineData("user@name", "使用者名稱只能包含字母、數字和底線")]
public void Validate_無效的使用者名稱_應該回傳對應錯誤訊息(string username, string expectedErrorMessage)
{
    // Arrange
    var request = CreateValidRequest();
    request.Username = username;

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.Username)
          .WithErrorMessage(expectedErrorMessage);
}
```

## 測試組織最佳實務

### 1. 測試組織

- **按功能分組**：每個驗證功能有獨立的測試類別
- **描述性命名**：測試方法名稱清楚說明測試情境和預期結果
- **3A 模式**：Arrange、Act、Assert 結構清晰

### 2. 邊界值測試

- **年齡邊界**：18歲、120歲
- **長度邊界**：字串長度的上下限
- **格式邊界**：正規表達式的邊界條件

### 3. 錯誤訊息驗證

- **精確匹配**：使用 `WithErrorMessage` 驗證錯誤訊息
- **一致性**：確保錯誤訊息符合使用者體驗要求

## 學習重點

1. **FluentValidation Test Extensions** 的使用方法
2. **參數化測試** 提升測試效率
3. **FakeTimeProvider** 控制時間相關的測試
4. **時間相關驗證** 的測試策略
5. **條件式驗證** 的測試技巧
6. **年齡與出生日期一致性** 驗證測試

這個專案展示了如何使用 FluentValidation Test Extensions 建立強健的驗證測試，專注於核心的驗證規則測試技術。
