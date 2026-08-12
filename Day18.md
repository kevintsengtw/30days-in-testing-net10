---
day: 18
title: "Day 18 - 驗證測試：FluentValidation Test Extensions"
sample: samples/day18
target_framework: net10.0
packages:
  - AwesomeAssertions
  - FluentValidation
  - Microsoft.Extensions.TimeProvider.Testing
  - Microsoft.Testing.Extensions.TrxReport
  - NSubstitute
  - xunit.v3.mtp-v2
  - xunit.runner.visualstudio
  - Microsoft.NET.Test.Sdk
---

# Day 18 - 驗證測試：FluentValidation Test Extensions

## 前言

前一篇處理檔案系統相依性，這一篇改測**資料驗證邏輯**。

在開發過程中，我們經常需要處理各種資料驗證：

- 使用者註冊資料驗證
- API 請求參數驗證
- 表單資料完整性檢查
- 業務規則驗證
- 複雜的跨欄位邏輯驗證

傳統上，我們可能會在業務邏輯中塞一堆 `if` 判斷來做驗證，或是寫一大串個別的驗證方法。但當驗證規則變複雜，或是需要寫完整測試時，就會碰到很多問題。

以下使用 **FluentValidation Test Extensions**，分別驗證單一欄位、邊界值與跨欄位規則。

---

## 資料驗證測試的根本挑戰

### 問題一：複雜的驗證規則組合

先看一個典型的使用者註冊驗證範例：

```csharp
/// <summary>
/// 使用者註冊請求資料
/// </summary>
public class UserRegistrationRequest
{
    /// <summary>
    /// 使用者名稱
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 電子郵件地址
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 密碼
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 確認密碼
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// 出生日期
    /// </summary>
    public DateTime BirthDate { get; set; }

    /// <summary>
    /// 年齡
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// 電話號碼
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// 使用者角色清單
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// 是否同意使用條款
    /// </summary>
    public bool AgreeToTerms { get; set; }
}
```

這個看起來簡單的註冊資料，可能包含以下驗證規則：

- **Username**：不可為 null、不可為空白、長度 3-20、只能包含字母數字和底線
- **Email**：不可為 null、不可為空白、有效格式、長度不超過 100、不能重複
- **Password**：不可為 null、不可為空白、長度 8-50、包含大小寫字母和數字
- **ConfirmPassword**：必須與 Password 相同
- **BirthDate** 和 **Age**：年齡必須與生日計算結果一致、必須年滿 18 歲
- **PhoneNumber**：可選、但如果填寫必須是有效格式
- **Roles**：不可為 null、不可為空的陣列、每個角色都必須存在於系統中
- **AgreeToTerms**：必須為 true

**測試的複雜度**：

```text
驗證規則數量：8 個主要欄位
每個欄位的規則：平均 3-4 條
條件組合：可能的測試案例數量呈指數成長
邊界條件：空值、極值、格式邊界、業務邏輯邊界
```

### 問題二：傳統驗證方式的測試困難

```csharp
public class UserRegistrationService
{
    public ValidationResult ValidateRegistration(UserRegistrationRequest request)
    {
        var errors = new List<string>();

        // 使用者名稱驗證
        if (string.IsNullOrEmpty(request.Username))
        {
            errors.Add("使用者名稱不可為 null 或空白");
        }
        else if (request.Username.Length < 3 || request.Username.Length > 20)
        {
            errors.Add("使用者名稱長度必須在 3 到 20 個字元之間");
        }
        else if (!Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_]+$"))
        {
            errors.Add("使用者名稱只能包含字母、數字和底線");
        }

        // 電子郵件驗證
        if (string.IsNullOrEmpty(request.Email))
        {
            errors.Add("電子郵件不可為 null 或空白");
        }
        else if (!IsValidEmail(request.Email))
        {
            errors.Add("電子郵件格式不正確");
        }

        // 密碼驗證
        if (string.IsNullOrEmpty(request.Password))
        {
            errors.Add("密碼不可為 null 或空白");
        }
        else if (request.Password.Length < 8)
        {
            errors.Add("密碼長度不能少於 8 個字元");
        }
        else if (!HasComplexPassword(request.Password))
        {
            errors.Add("密碼必須包含大小寫字母和數字");
        }

        // ... 更多驗證邏輯

        return new ValidationResult(errors);
    }
}
```

這種驗證方式在測試時會遇到：

1. **測試案例爆炸**：每個組合都需要獨立測試
2. **錯誤訊息難以維護**：散布在各處的字串常數
3. **邏輯重複**：相似的驗證邏輯在不同地方重複
4. **測試複雜度高**：需要為每個分支寫測試

### 問題三：跨欄位驗證的複雜性

```csharp
// 年齡與生日的一致性驗證
if (request.BirthDate != default && request.Age > 0)
{
    var calculatedAge = DateTime.Now.Year - request.BirthDate.Year;
    if (request.BirthDate.Date > DateTime.Now.AddYears(-calculatedAge))
    {
        calculatedAge--;
    }

    if (calculatedAge != request.Age)
    {
        errors.Add("年齡與生日不一致");
    }
}
```

這種跨欄位驗證邏輯：

- **難以測試**：需要控制時間
- **邏輯複雜**：容易出錯
- **維護困難**：業務規則變更時需要同步修改測試

### 問題四：條件式驗證

```csharp
// 電話號碼是可選的，但如果填了就必須是有效格式
if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
{
    if (!Regex.IsMatch(request.PhoneNumber, @"^09\d{8}$"))
    {
        errors.Add("電話號碼格式不正確");
    }
}
```

條件式驗證增加了測試的複雜度，需要測試：

- 空值情況（跳過驗證）
- 有值但無效的情況
- 有值且有效的情況

---

## FluentValidation 與 Test Extensions 解決方案

### 什麼是 FluentValidation？

**FluentValidation** 是 .NET 的驗證框架，用強型別的方式定義驗證規則。它採流暢介面（Fluent Interface）設計，規則寫出來接近一句英文，讀的人不必猜。

### 為什麼要使用 FluentValidation？

#### 傳統 DataAnnotation 的限制

在 ASP.NET Core 中，我們通常會使用 **DataAnnotation** 來做模型驗證：

```csharp
/// <summary>
/// 使用者註冊請求資料
/// </summary>
public class UserRegistrationRequest
{
    /// <summary>
    /// 使用者名稱
    /// </summary>
    [Required(ErrorMessage = "使用者名稱不可為 null 或空白")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "使用者名稱長度必須在 3 到 20 個字元之間")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "使用者名稱只能包含字母、數字和底線")]
    public string Username { get; set; }

    /// <summary>
    /// 電子郵件地址
    /// </summary>
    [Required(ErrorMessage = "電子郵件不可為 null 或空白")]
    [EmailAddress(ErrorMessage = "電子郵件格式不正確")]
    [StringLength(100, ErrorMessage = "電子郵件長度不能超過 100 個字元")]
    public string Email { get; set; }

    /// <summary>
    /// 密碼
    /// </summary>
    [Required(ErrorMessage = "密碼不可為 null 或空白")]
    [StringLength(50, MinimumLength = 8, ErrorMessage = "密碼長度必須在 8 到 50 個字元之間")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "密碼必須包含大小寫字母和數字")]
    public string Password { get; set; }

    /// <summary>
    /// 確認密碼
    /// </summary>
    [Required(ErrorMessage = "確認密碼不可為 null 或空白")]
    [Compare("Password", ErrorMessage = "確認密碼必須與密碼相同")]
    public string ConfirmPassword { get; set; }

    /// <summary>
    /// 年齡
    /// </summary>
    [Range(18, 120, ErrorMessage = "年齡必須在 18 到 120 歲之間")]
    public int Age { get; set; }

    /// <summary>
    /// 出生日期（如何驗證年齡與生日的一致性？DataAnnotation 很困難！）
    /// </summary>
    public DateTime BirthDate { get; set; }
}
```

**DataAnnotation 的問題：**

1. **複雜邏輯困難**：跨欄位驗證、條件式驗證很難實現
2. **可讀性差**：複雜的正規表達式和驗證邏輯混在屬性上
3. **重用性低**：驗證邏輯綁定在模型上，難以在不同地方重用
4. **測試困難**：無法單獨測試驗證邏輯
5. **有限的驗證器**：內建驗證器有限，自訂驗證器複雜
6. **錯誤訊息管理**：錯誤訊息散布在各處，難以統一管理

#### FluentValidation 的優勢

```csharp
public class UserRegistrationValidator : AbstractValidator<UserRegistrationRequest>
{
    public UserRegistrationValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("使用者名稱不可為 null 或空白")
            .Length(3, 20).WithMessage("使用者名稱長度必須在 3 到 20 個字元之間")
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("使用者名稱只能包含字母、數字和底線");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("電子郵件不可為 null 或空白")
            .EmailAddress().WithMessage("電子郵件格式不正確")
            .MaximumLength(100).WithMessage("電子郵件長度不能超過 100 個字元");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密碼不可為 null 或空白")
            .Length(8, 50).WithMessage("密碼長度必須在 8 到 50 個字元之間")
            .Must(BeComplexPassword).WithMessage("密碼必須包含大小寫字母和數字");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("確認密碼必須與密碼相同");

        // 複雜的跨欄位驗證變得簡單！
        RuleFor(x => x.BirthDate)
            .Must((request, birthDate) => IsAgeConsistentWithBirthDate(birthDate, request.Age, timeProvider))
            .WithMessage("生日與年齡不一致");

        // 條件式驗證也很容易
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^09\d{8}$").WithMessage("電話號碼格式不正確")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }

    private bool BeComplexPassword(string password)
    {
        return !string.IsNullOrEmpty(password) &&
               Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$");
    }

    private bool IsAgeConsistentWithBirthDate(DateTime birthDate, int age, TimeProvider timeProvider)
    {
        var currentDate = timeProvider.GetLocalNow().Date;
        var calculatedAge = currentDate.Year - birthDate.Year;

        if (birthDate.Date > currentDate.AddYears(-calculatedAge))
        {
            calculatedAge--;
        }

        return calculatedAge == age;
    }
}
```

### FluentValidation vs DataAnnotation 比較

| 特性             | DataAnnotation       | FluentValidation   |
| ---------------- | -------------------- | ------------------ |
| **複雜驗證邏輯** | 困難，需要自訂屬性   | 簡單，用方法撰寫   |
| **跨欄位驗證**   | 非常困難             | 原生支援           |
| **條件式驗證**   | 幾乎不可能           | 用 `When` 輕鬆實現 |
| **可讀性**       | 屬性堆疊，難讀       | 流暢介面，清晰     |
| **可測試性**     | 難以單獨測試         | 專門的測試工具     |
| **重用性**       | 綁定在模型上         | 獨立的驗證器類別   |
| **客制化**       | 需要寫屬性類別       | 直接寫方法邏輯     |
| **錯誤訊息**     | 散布各處             | 集中管理           |
| **非同步驗證**   | 不支援               | 原生支援           |
| **相依性注入**   | 困難                 | 完全支援           |

### FluentValidation 相關資源

在開始使用 FluentValidation 之前，建議先了解以下資源：

- **官方文件**：[FluentValidation Documentation](https://docs.fluentvalidation.net/)
- **GitHub 專案**：[FluentValidation GitHub Repository](https://github.com/FluentValidation/FluentValidation)
- **NuGet 套件**：[FluentValidation NuGet Package](https://www.nuget.org/packages/FluentValidation/)（測試輔助的 `FluentValidation.TestHelper` 命名空間隨主套件提供，不是獨立套件）
- **ASP.NET Core 整合**：[FluentValidation.AspNetCore NuGet Package](https://www.nuget.org/packages/FluentValidation.AspNetCore/)——**注意：此套件已停止維護**，官方不建議新專案採用（自動驗證管線也無法執行 async 規則）；新專案請用核心 FluentValidation 套件搭配手動驗證，詳見[官方說明](https://docs.fluentvalidation.net/en/latest/aspnet.html)

### FluentValidation 基礎特色

FluentValidation 是個流行的 .NET 驗證框架，它有這些特色：

- **流暢的 API**：用方法鏈的方式定義驗證規則
- **分離關注點**：把驗證邏輯從業務邏輯中分開
- **可組合性**：可以輕鬆組合和重用驗證規則
- **豐富的內建驗證器**：常見的驗證需求都有現成解決方案
- **完整的測試支援**：提供專門的測試工具和輔助方法

### 測試專案安裝必要套件

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <IsTestProject>true</IsTestProject>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <OutputType>Exe</OutputType>
    </PropertyGroup>

    <ItemGroup>
        <!-- 主要驗證框架（TestHelper 測試輔助類別隨主套件提供，不需另外安裝） -->
        <PackageReference Include="FluentValidation"/>

        <!-- 測試相關套件 -->
        <PackageReference Include="AwesomeAssertions"/>
        <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing"/>
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport"/>
        <PackageReference Include="NSubstitute"/>
        <PackageReference Include="xunit.v3.mtp-v2"/>

        <!-- IDE 測試總管相容：Rider 的 xUnit 探索需要 Microsoft.NET.Test.Sdk，VSTest 路徑另需 xunit.runner.visualstudio -->
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="xunit.runner.visualstudio" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\ValidationExample.Core\ValidationExample.Core.csproj"/>
    </ItemGroup>

</Project>
```

> 套件版本集中在範例專案的 `Directory.Packages.props`（CPM）管理，`.csproj` 不寫版本號。

---

## 重構驗證邏輯：從 DataAnnotation 到 FluentValidation

### 重構前：使用 DataAnnotation（不易測試）

```csharp
// 模型定義：驗證邏輯與資料模型混合
public class UserRegistrationRequest
{
    [Required(ErrorMessage = "使用者名稱不可為 null 或空白")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "使用者名稱長度必須在 3 到 20 個字元之間")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "使用者名稱只能包含字母、數字和底線")]
    public string Username { get; set; }

    [Required(ErrorMessage = "電子郵件不可為 null 或空白")]
    [EmailAddress(ErrorMessage = "電子郵件格式不正確")]
    [StringLength(100, ErrorMessage = "電子郵件長度不能超過 100 個字元")]
    public string Email { get; set; }

    [Required(ErrorMessage = "密碼不可為 null 或空白")]
    [StringLength(50, MinimumLength = 8, ErrorMessage = "密碼長度必須在 8 到 50 個字元之間")]
    public string Password { get; set; }

    [Required(ErrorMessage = "確認密碼不可為 null 或空白")]
    [Compare("Password", ErrorMessage = "確認密碼必須與密碼相同")]
    public string ConfirmPassword { get; set; }

    [Range(18, 120, ErrorMessage = "年齡必須在 18 到 120 歲之間")]
    public int Age { get; set; }

    // DataAnnotation 無法簡單處理跨欄位驗證
    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; }
    public List<string> Roles { get; set; }
    public bool AgreeToTerms { get; set; }
}

// 傳統服務驗證：混亂的 if 判斷
public class UserRegistrationService
{
    public ValidationResult ValidateRegistration(UserRegistrationRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(request.Username))
        {
            errors.Add("使用者名稱不可為 null 或空白");
        }

        if (string.IsNullOrEmpty(request.Email))
        {
            errors.Add("電子郵件不可為 null 或空白");
        }

        // ... 更多散亂的驗證邏輯

        return new ValidationResult(errors);
    }
}
```

### 重構後：使用 FluentValidation（高度可測試）

```csharp
// 乾淨的資料模型：只專注於資料結構
/// <summary>
/// 使用者註冊請求資料
/// </summary>
public class UserRegistrationRequest
{
    /// <summary>
    /// 使用者名稱
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 電子郵件地址
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 密碼
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 確認密碼
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// 出生日期
    /// </summary>
    public DateTime BirthDate { get; set; }

    /// <summary>
    /// 年齡
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// 電話號碼
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// 使用者角色清單
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// 是否同意使用條款
    /// </summary>
    public bool AgreeToTerms { get; set; }
}
```

專門的驗證器：邏輯清晰且可測試

```csharp
using System.Text.RegularExpressions;
using FluentValidation;

/// <summary>
/// 使用者註冊請求驗證器
/// </summary>
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

    /// <summary>
    /// 設定所有驗證規則
    /// </summary>
    private void SetupValidationRules()
    {
        // 使用者名稱驗證
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("使用者名稱不可為 null 或空白")
            .Length(3, 20).WithMessage("使用者名稱長度必須在 3 到 20 個字元之間")
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("使用者名稱只能包含字母、數字和底線");

        // 電子郵件驗證
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("電子郵件不可為 null 或空白")
            .EmailAddress().WithMessage("電子郵件格式不正確")
            .MaximumLength(100).WithMessage("電子郵件長度不能超過 100 個字元");

        // 密碼驗證
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密碼不可為 null 或空白")
            .Length(8, 50).WithMessage("密碼長度必須在 8 到 50 個字元之間")
            .Must(BeComplexPassword).WithMessage("密碼必須包含大小寫字母和數字");

        // 確認密碼驗證
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("確認密碼必須與密碼相同");

        // 年齡驗證
        RuleFor(x => x.Age)
            .GreaterThanOrEqualTo(18).WithMessage("年齡必須大於或等於 18 歲")
            .LessThanOrEqualTo(120).WithMessage("年齡必須小於或等於 120 歲");

        // 生日與年齡一致性驗證
        RuleFor(x => x.BirthDate)
            .Must((request, birthDate) => IsAgeConsistentWithBirthDate(birthDate, request.Age))
            .WithMessage("生日與年齡不一致");

        // 電話號碼驗證（可選）
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^09\d{8}$").WithMessage("電話號碼格式不正確")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        // 角色驗證
        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("至少需要指定一個角色")
            .Must(roles => roles == null || roles.All(role => IsValidRole(role)))
            .WithMessage("包含無效的角色");

        // 同意條款驗證
        RuleFor(x => x.AgreeToTerms)
            .Equal(true).WithMessage("必須同意使用條款");
    }

    private bool BeComplexPassword(string password)
    {
        return !string.IsNullOrEmpty(password) &&
               Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$");
    }

    /// <summary>
    /// 檢查年齡是否與生日一致
    /// </summary>
    private bool IsAgeConsistentWithBirthDate(DateTime birthDate, int age)
    {
        var currentDate = _timeProvider.GetLocalNow().Date;
        var calculatedAge = currentDate.Year - birthDate.Year;

        if (birthDate.Date > currentDate.AddYears(-calculatedAge))
        {
            calculatedAge--;
        }

        return calculatedAge == age;
    }

    /// <summary>
    /// 檢查角色是否有效
    /// </summary>
    private bool IsValidRole(string role)
    {
        var validRoles = new[] { "User", "Admin", "Manager", "Support" };
        return validRoles.Contains(role);
    }
}
```

---

## 為什麼要對 Validator 類別做單元測試？

開始寫測試前，先釐清為什麼驗證器值得獨立測試。

### 驗證邏輯的重要性

驗證器是應用程式的第一道防線，它們負責：

1. **資料完整性保護**：確保進入系統的資料符合業務規則
2. **安全性把關**：防止惡意或不當的資料輸入
3. **使用者體驗**：提供清楚的錯誤訊息指導使用者
4. **系統穩定性**：避免無效資料造成後續處理錯誤

**如果驗證邏輯出錯，後果可能很嚴重：**

```csharp
// 想像一下這些驗證失效的後果：
- 使用者可以註冊空白的使用者名稱
- 系統接受格式錯誤的電子郵件地址
- 密碼複雜度要求被繞過
- 未成年使用者可以成功註冊成人服務
- 無效的角色權限被授予使用者
```

### 對 Validator 做單元測試的核心好處

#### 1. **業務規則的活文件**

測試案例本身就是最好的業務規則文件：

```csharp
[Fact]
public void Validate_未成年使用者_應該驗證失敗()
{
    // 這個測試明確說明：系統不允許未成年使用者註冊
    // 比任何文件都更清楚、更準確
}

[Theory]
[InlineData(17, "年齡必須大於或等於 18 歲")]
public void Validate_年齡邊界值_應該正確驗證(int age, string expectedError)
{
    // 清楚定義了年齡限制的邊界條件
}
```

#### 2. **驗證邏輯的獨立性驗證**

驗證器可以脫離 Web 框架、資料庫、外部服務獨立測試：

```csharp
[Fact]
public void Validate_複雜密碼規則_不依賴任何外部系統()
{
    // Arrange - 純粹的記憶體物件
    var validator = new UserRegistrationValidator(_fakeTimeProvider);
    var request = new UserRegistrationRequest { Password = "weak" };

    // Act - 純粹的邏輯運算
    var result = validator.TestValidate(request);

    // Assert - 即時驗證結果
    result.ShouldHaveValidationErrorFor(x => x.Password);

    // 執行速度快、結果穩定、不受外部影響
}
```

#### 3. **邊界條件和例外情況的完整涵蓋**

驗證器測試可以系統性地測試各種邊界條件：

```csharp
[Theory]
[InlineData("")]           // 空字串
[InlineData(null)]         // null 值
[InlineData("ab")]         // 太短
[InlineData("a")]          // 極短
[InlineData("very_long_username_that_exceeds_limit")] // 太長
[InlineData("user@name")]  // 包含特殊字元
[InlineData("user name")]  // 包含空格
public void Validate_使用者名稱邊界條件_應該正確處理(string username)
{
    // 系統性地測試各種可能的輸入情況
}
```

#### 4. **重構安全網**

當業務規則變更時，測試提供安全的重構保障：

```csharp
// 原本的業務規則：年齡必須 >= 18
[Theory]
[InlineData(17, false)]  // 未成年
[InlineData(18, true)]   // 成年

// 如果業務規則改變：年齡必須 >= 16
// 測試會立即告訴我們哪些地方需要修改
```

#### 5. **跨欄位驗證的可靠性**

複雜的跨欄位邏輯特別需要測試保護：

```csharp
[Fact]
public void Validate_年齡與生日一致性_複雜邏輯需要測試保護()
{
    // 這種邏輯容易出錯，測試提供保障
    var request = CreateValidRequest();
    request.BirthDate = new DateTime(1990, 6, 15);  // 生日在今年尚未到達
    request.Age = 33;  // 正確年齡計算

    var result = _validator.TestValidate(request);

    result.ShouldNotHaveValidationErrorFor(x => x.Age);
}
```

#### 6. **效能和可維護性**

- **快速執行**：驗證器測試通常在毫秒內完成
- **易於維護**：測試程式碼結構清晰，容易修改
- **自動化友善**：可以整合到 CI/CD 流程中
- **回歸防護**：防止修改時意外破壞現有功能

### 測試涵蓋率的實際價值

對驗證器做完整測試涵蓋有具體的商業價值：

1. **減少正式環境錯誤**：提早發現驗證漏洞
2. **降低客服成本**：減少因驗證問題導致的使用者困惑
3. **提升開發信心**：開發團隊可以安心修改驗證規則
4. **加速功能開發**：新功能可以快速驗證其驗證邏輯正確性
5. **合規性要求**：某些行業要求驗證邏輯有完整的測試證明

### 小結

驗證器測試應涵蓋有效值、邊界值、無效值與跨欄位規則。這些案例能把輸入限制固定成可執行的規格，規則調整時也能立即看出受影響的行為。

---

## FluentValidation Test Extensions 基本用法

### 基礎測試寫法

FluentValidation Test Extensions 有專門的測試輔助方法：

```csharp
using FluentValidation.TestHelper;
using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// 使用者註冊驗證器測試
/// </summary>
public class UserRegistrationValidatorTests
{
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly UserRegistrationValidator _validator;

    public UserRegistrationValidatorTests()
    {
        // 設定固定的測試時間 (2024/1/1)
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _validator = new UserRegistrationValidator(_fakeTimeProvider);
    }

    [Theory]
    [InlineData("validuser123")]
    [InlineData("user_name")]
    [InlineData("User123")]
    [InlineData("test_user_456")]
    [InlineData("user123")]
    public void Validate_有效的使用者名稱_應該通過驗證(string username)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = username;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Validate_空的使用者名稱_應該驗證失敗()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = "";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("使用者名稱不可為 null 或空白");
    }

    [Fact]
    public void Validate_太短的使用者名稱_應該驗證失敗()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = "ab";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("使用者名稱長度必須在 3 到 20 個字元之間");
    }

    [Fact]
    public void Validate_太長的使用者名稱_應該驗證失敗()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = "this_is_a_very_long_username_that_exceeds_the_limit";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("使用者名稱長度必須在 3 到 20 個字元之間");
    }

    [Fact]
    public void Validate_包含特殊字元的使用者名稱_應該驗證失敗()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = "user@name";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("使用者名稱只能包含字母、數字和底線");
    }

    /// <summary>
    /// 建立有效的測試請求資料
    /// </summary>
    /// <returns>有效的使用者註冊請求</returns>
    private UserRegistrationRequest CreateValidRequest()
    {
        return new UserRegistrationRequest
        {
            Username = "testuser123",
            Email = "test@example.com",
            Password = "TestPass123",
            ConfirmPassword = "TestPass123",
            BirthDate = new DateTime(1990, 1, 1),
            Age = 34, // 2024 - 1990 = 34
            PhoneNumber = "0912345678",
            Roles = new List<string> { "User" },
            AgreeToTerms = true
        };
    }
}
```

### 核心測試方法

**`ShouldHaveValidationErrorFor`**：

- 驗證特定屬性應該有驗證錯誤
- 可以串接 `WithErrorMessage` 驗證錯誤訊息
- 可以串接 `WithErrorCode` 驗證錯誤程式碼

**`ShouldNotHaveValidationErrorFor`**：

- 驗證特定屬性不應該有驗證錯誤
- 用於驗證正確的資料通過驗證

**測試多個欄位的範例**：

```csharp
[Fact]
public void Validate_密碼相關欄位_應該有正確的驗證結果()
{
    // Arrange
    var request = CreateValidRequest();
    request.Password = "weak";
    request.ConfirmPassword = "different";

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.Password);
    result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
}
```

---

## 進階驗證技術

### Theory 測試與參數化驗證

使用 `Theory` 可以有效測試多種輸入組合：

```csharp
[Theory]
[InlineData("", "使用者名稱不可為 null 或空白")]
[InlineData(null, "使用者名稱不可為 null 或空白")]
[InlineData("a", "使用者名稱長度必須在 3 到 20 個字元之間")]
[InlineData("ab", "使用者名稱長度必須在 3 到 20 個字元之間")]
[InlineData("this_is_a_very_long_username_that_exceeds_the_limit", "使用者名稱長度必須在 3 到 20 個字元之間")]
[InlineData("user@name", "使用者名稱只能包含字母、數字和底線")]
[InlineData("user name", "使用者名稱只能包含字母、數字和底線")]
public void Validate_無效的使用者名稱_應該回傳對應錯誤訊息(string? username, string expectedErrorMessage)
{
    // Arrange
    var request = CreateValidRequest();
    request.Username = username!;

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.Username)
          .WithErrorMessage(expectedErrorMessage);
}

[Theory]
[InlineData("validuser123")]
[InlineData("user_name")]
[InlineData("User123")]
[InlineData("test_user_456")]
[InlineData("user123")]
public void Validate_有效的使用者名稱_應該通過驗證(string username)
{
    // Arrange
    var request = CreateValidRequest();
    request.Username = username;

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Username);
}
```

### 電子郵件驗證測試

```csharp
[Theory]
[InlineData("", "電子郵件不可為 null 或空白")]
[InlineData("invalid", "電子郵件格式不正確")]
[InlineData("invalid@", "電子郵件格式不正確")]
[InlineData("@example.com", "電子郵件格式不正確")]
public void Validate_無效的電子郵件_應該驗證失敗(string email, string expectedErrorMessage)
{
    // Arrange
    var request = CreateValidRequest();
    request.Email = email;

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.Email)
          .WithErrorMessage(expectedErrorMessage);
}

[Fact]
public void Validate_過長的電子郵件_應該驗證失敗()
{
    // Arrange
    var request = CreateValidRequest();
    request.Email = new string('a', 92) + "@test.com"; // 92 + 1 + 8 = 101，超過 100 的上限

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.Email)
          .WithErrorMessage("電子郵件長度不能超過 100 個字元");
}

[Theory]
[InlineData("test@example.com")]
[InlineData("user.name@domain.co.uk")]
[InlineData("firstname+lastname@company.org")]
public void Validate_有效的電子郵件_應該通過驗證(string email)
{
    // Arrange
    var request = CreateValidRequest();
    request.Email = email;

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Email);
}
```

### 密碼複雜度驗證

```csharp
[Theory]
[InlineData("weak", "密碼長度必須在 8 到 50 個字元之間")]
[InlineData("weakpass", "密碼必須包含大小寫字母和數字")]
[InlineData("WEAKPASS123", "密碼必須包含大小寫字母和數字")]
[InlineData("weakpass123", "密碼必須包含大小寫字母和數字")]
[InlineData("WeakPass", "密碼必須包含大小寫字母和數字")]
public void Validate_不符合複雜度要求的密碼_應該驗證失敗(string password, string expectedErrorMessage)
{
    // Arrange
    var request = CreateValidRequest();
    request.Password = password;
    request.ConfirmPassword = password;

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.Password)
          .WithErrorMessage(expectedErrorMessage);
}

[Theory]
[InlineData("Password123")]
[InlineData("MySecure1")]
[InlineData("StrongPass99")]
public void Validate_符合複雜度要求的密碼_應該通過驗證(string password)
{
    // Arrange
    var request = CreateValidRequest();
    request.Password = password;
    request.ConfirmPassword = password;

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Password);
}

[Fact]
public void Validate_密碼與確認密碼不一致_應該驗證失敗()
{
    // Arrange
    var request = CreateValidRequest();
    request.Password = "Password123";
    request.ConfirmPassword = "DifferentPass456";

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
          .WithErrorMessage("確認密碼必須與密碼相同");
}
```

---

## 年齡與生日驗證

### 基本年齡驗證

```csharp
[Theory]
[InlineData(17, "年齡必須大於或等於 18 歲")]   // 邊界下限 - 1
[InlineData(121, "年齡必須小於或等於 120 歲")] // 邊界上限 + 1
public void Validate_年齡邊界值_應該正確驗證(int age, string expectedError)
{
    // Arrange
    // 設定測試當下的時間為 2024/1/1
    _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

    var request = CreateValidRequest();
    request.Age = age;
    // 設定對應的出生日期，避免跨欄位驗證錯誤
    if (age == 17)
    {
        request.BirthDate = new DateTime(2006, 6, 1); // 在 2024/1/1 時還未滿 18 歲
    }
    else if (age == 121)
    {
        request.BirthDate = new DateTime(1902, 1, 1); // 121 歲
    }

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.Age)
          .WithErrorMessage(expectedError);
}

    [Theory]
    [InlineData(18)]  // 邊界下限
    [InlineData(120)] // 邊界上限
    [InlineData(25)]  // 一般情況
    public void Validate_有效年齡_應該通過驗證(int age)
    {
        // Arrange
        // 設定測試當下的時間為 2024/1/1
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var request = CreateValidRequest();
        request.Age = age;
        request.BirthDate = new DateTime(2024 - age, 1, 1); // 確保年齡與生日一致

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Age);
        // 年齡與生日的一致性規則掛在 BirthDate 上，必須一併斷言才能守住跨欄位規則
        result.ShouldNotHaveValidationErrorFor(x => x.BirthDate);
    }

[Fact]
public void Validate_年齡與出生日期不一致_應該驗證失敗()
{
    // Arrange
    // 設定測試當下的時間為 2024/5/12
    _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 5, 12, 0, 0, 0, TimeSpan.Zero));

    var request = CreateValidRequest();
    request.BirthDate = new DateTime(2000, 1, 1); // 出生日期顯示應該是 24 歲
    request.Age = 29;                             // 但是設定年齡為 29 歲，不一致

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldHaveValidationErrorFor(x => x.BirthDate)
          .WithErrorMessage("生日與年齡不一致");
}

[Fact]
public void Validate_年齡與出生日期一致且滿18歲_應該通過驗證()
{
    // Arrange
    // 設定測試當下的時間為 2024/5/12
    _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 5, 12, 0, 0, 0, TimeSpan.Zero));

    var request = CreateValidRequest();
    request.BirthDate = new DateTime(2000, 1, 1); // 出生日期
    request.Age = 24;                             // 在 2024/5/12 時正確的年齡

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.BirthDate);
    result.ShouldNotHaveValidationErrorFor(x => x.Age);
}
```

### 時間處理的重要考量

在年齡驗證的實作中，我們使用 `GetLocalNow()` 而非 `GetUtcNow()`，這是一個重要的設計決策：

#### 為什麼使用本地時間？

- **業務邏輯正確性**：使用者輸入的生日是基於當地時區的日期
- **避免時區問題**：UTC 時間可能導致年齡計算錯誤，特別是跨日期邊界時
- **使用者體驗一致性**：年齡計算應該符合使用者的預期

#### 測試時的時間設定

雖然我們在驗證器中使用 `GetLocalNow()`，但在測試時仍使用 `SetUtcNow()` 來設定 FakeTimeProvider 的時間。這是因為：

- FakeTimeProvider 內部會根據系統時區自動轉換為本地時間
- 測試的重點是驗證邏輯正確性，而非時區轉換功能
- 保持測試程式碼的簡潔性

> **延伸學習**：在 `Day 16 - 測試日期與時間：Microsoft.Bcl.TimeProvider 取代 DateTime` 中，我們介紹了 `SetLocalNow()` 擴充方法，可以更直觀地設定本地時間：
>
> ```csharp
> public static class FakeTimeProviderExtensions
> {
>     public static void SetLocalNow(this FakeTimeProvider fakeTimeProvider, DateTime localDateTime)
>     {
>         fakeTimeProvider.SetLocalTimeZone(TimeZoneInfo.Local);
>         var utcTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, TimeZoneInfo.Local);
>         fakeTimeProvider.SetUtcNow(utcTime);
>     }
> }
> ```
>
> 在 FluentValidation 測試中，我們選擇直接使用 `SetUtcNow()` 是為了保持範例的簡潔性，但在實際專案中，建議使用 `SetLocalNow()` 擴充方法來提高程式碼的可讀性。

### 使用 FakeTimeProvider 的時間控制

```csharp
[Fact]
public void Validate_使用FakeTimeProvider_應該正確計算年齡()
{
    // Arrange
    // 設定測試時間為 2024 年 6 月 15 日
    _fakeTimeProvider.SetUtcNow(new DateTime(2024, 6, 15));
    var validator = new UserRegistrationValidator(_fakeTimeProvider);

    var request = CreateValidRequest();
    request.BirthDate = new DateTime(1990, 3, 10); // 生日在今年已過
    request.Age = 34; // 2024 - 1990 = 34

    // Act
    var result = validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Age);
    // 一致性規則掛在 BirthDate 上，一併斷言才真正驗到時間計算
    result.ShouldNotHaveValidationErrorFor(x => x.BirthDate);
}

[Fact]
public void Validate_生日尚未到達_年齡計算應該正確()
{
    // Arrange
    // 設定測試時間為 2024 年 2 月 1 日
    _fakeTimeProvider.SetUtcNow(new DateTime(2024, 2, 1));
    var validator = new UserRegistrationValidator(_fakeTimeProvider);

    var request = CreateValidRequest();
    request.BirthDate = new DateTime(1990, 6, 15); // 生日在今年尚未到達
    request.Age = 33; // 2024 - 1990 - 1 = 33 (生日未到，需減一歲)

    // Act
    var result = validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Age);
    // 一致性規則掛在 BirthDate 上，一併斷言才真正驗到時間計算
    result.ShouldNotHaveValidationErrorFor(x => x.BirthDate);
}

[Fact]
public void Validate_跨年份測試_年齡計算應該正確()
{
    // Arrange
    // 設定測試時間為 2024 年 12 月 31 日
    _fakeTimeProvider.SetUtcNow(new DateTime(2024, 12, 31));
    var validator = new UserRegistrationValidator(_fakeTimeProvider);

    var request = CreateValidRequest();
    request.BirthDate = new DateTime(1990, 1, 1);
    request.Age = 34; // 2024 - 1990 = 34

    // Act
    var result = validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.Age);
    // 一致性規則掛在 BirthDate 上，一併斷言才真正驗到時間計算
    result.ShouldNotHaveValidationErrorFor(x => x.BirthDate);
}
```

---

## 進階驗證情境與 NSubstitute 整合

### 自訂驗證規則測試

當驗證需要依賴外部服務時，我們需要使用 Mock 物件：

```csharp
using NSubstitute;

/// <summary>
/// 使用者服務介面
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 檢查使用者名稱是否可用
    /// </summary>
    /// <param name="username">使用者名稱</param>
    /// <returns>是否可用</returns>
    Task<bool> IsUsernameAvailableAsync(string username);

    /// <summary>
    /// 檢查電子郵件是否已註冊
    /// </summary>
    /// <param name="email">電子郵件</param>
    /// <returns>是否已註冊</returns>
    Task<bool> IsEmailRegisteredAsync(string email);
}

/// <summary>
/// 使用者註冊請求非同步驗證器
/// </summary>
public class UserRegistrationAsyncValidator : AbstractValidator<UserRegistrationRequest>
{
    private readonly IUserService _userService;
    private readonly TimeProvider _timeProvider;

    public UserRegistrationAsyncValidator(IUserService userService) : this(userService, TimeProvider.System)
    {
    }

    public UserRegistrationAsyncValidator(IUserService userService, TimeProvider timeProvider)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ConfigureValidationRules();
    }

    private void ConfigureValidationRules()
    {
        // 基本規則 (同步驗證)，其餘欄位規則與同步版驗證器相同，此處節錄
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("使用者名稱不能為空")
            .Length(3, 20).WithMessage("使用者名稱長度必須在 3 到 20 個字元之間")
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("使用者名稱只能包含字母、數字和底線");

        // 非同步驗證規則
        RuleFor(x => x.Username)
            .MustAsync(async (username, cancellation) =>
                           await _userService.IsUsernameAvailableAsync(username))
            .WithMessage("使用者名稱已被使用");

        RuleFor(x => x.Email)
            .MustAsync(async (email, cancellation) =>
                           !await _userService.IsEmailRegisteredAsync(email))
            .WithMessage("此電子郵件已被註冊");
    }
}
```

### 非同步驗證測試

```csharp
public class UserRegistrationAsyncValidatorTests
{
    private readonly IUserService _mockUserService;
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly UserRegistrationAsyncValidator _validator;

    public UserRegistrationAsyncValidatorTests()
    {
        _mockUserService = Substitute.For<IUserService>();
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _validator = new UserRegistrationAsyncValidator(_mockUserService, _fakeTimeProvider);
    }

    [Fact]
    public async Task ValidateAsync_使用者名稱可用_應該通過驗證()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = "newuser123";

        _mockUserService.IsUsernameAvailableAsync("newuser123")
                       .Returns(Task.FromResult(true));
        _mockUserService.IsEmailRegisteredAsync(Arg.Any<string>())
                       .Returns(Task.FromResult(false));

        // Act
        var result = await _validator.TestValidateAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
        await _mockUserService.Received(1).IsUsernameAvailableAsync("newuser123");
    }

    [Fact]
    public async Task ValidateAsync_使用者名稱已被使用_應該驗證失敗()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = "existinguser";

        _mockUserService.IsUsernameAvailableAsync("existinguser")
                       .Returns(Task.FromResult(false));
        _mockUserService.IsEmailRegisteredAsync(Arg.Any<string>())
                       .Returns(Task.FromResult(false));

        // Act
        var result = await _validator.TestValidateAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username)
              .WithErrorMessage("使用者名稱已被使用");
        await _mockUserService.Received(1).IsUsernameAvailableAsync("existinguser");
    }

    [Fact]
    public async Task ValidateAsync_電子郵件已註冊_應該驗證失敗()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Email = "existing@example.com";

        _mockUserService.IsUsernameAvailableAsync(Arg.Any<string>())
                       .Returns(Task.FromResult(true));
        _mockUserService.IsEmailRegisteredAsync("existing@example.com")
                       .Returns(Task.FromResult(true));

        // Act
        var result = await _validator.TestValidateAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("此電子郵件已被註冊");
        await _mockUserService.Received(1).IsEmailRegisteredAsync("existing@example.com");
    }

    [Fact]
    public async Task ValidateAsync_外部服務拋出例外_應該正確處理()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = "testuser";

        _mockUserService.IsUsernameAvailableAsync("testuser")
                       .Returns(Task.FromException<bool>(new TimeoutException("服務逾時")));

        // Act & Assert
        var act = async () => await _validator.TestValidateAsync(request);
        await act.Should().ThrowAsync<TimeoutException>()
                 .WithMessage("服務逾時");

        await _mockUserService.Received(1).IsUsernameAvailableAsync("testuser");
    }

    private UserRegistrationRequest CreateValidRequest()
    {
        return new UserRegistrationRequest
        {
            Username = "testuser123",
            Email = "test@example.com",
            Password = "TestPass123",
            ConfirmPassword = "TestPass123",
            BirthDate = new DateTime(1990, 1, 1),
            Age = 34,
            PhoneNumber = "0912345678",
            Roles = new List<string> { "User" },
            AgreeToTerms = true
        };
    }
}
```

### 條件式驗證測試

有時我們需要按照特定條件來決定要不要跑驗證：

```csharp
public class ConditionalValidationTests
{
    private readonly UserRegistrationValidator _validator;

    public ConditionalValidationTests()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTime(2024, 1, 1));
        _validator = new UserRegistrationValidator(fakeTimeProvider);
    }

    [Fact]
    public void Validate_電話號碼為空字串_應該跳過驗證()
    {
        // Arrange
        var request = CreateValidRequest();
        request.PhoneNumber = "";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void Validate_電話號碼為null_應該跳過驗證()
    {
        // Arrange
        var request = CreateValidRequest();
        request.PhoneNumber = null;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void Validate_電話號碼為空格_應該跳過驗證()
    {
        // Arrange
        var request = CreateValidRequest();
        request.PhoneNumber = "   ";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void Validate_電話號碼格式不正確_應該產生驗證錯誤()
    {
        // Arrange
        var request = CreateValidRequest();
        request.PhoneNumber = "123456789"; // 不符合 09xxxxxxxx 格式

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber)
              .WithErrorMessage("電話號碼格式不正確");
    }

    [Fact]
    public void Validate_電話號碼格式正確_應該通過驗證()
    {
        // Arrange
        var request = CreateValidRequest();
        request.PhoneNumber = "0912345678"; // 正確的手機號碼格式

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    private UserRegistrationRequest CreateValidRequest()
    {
        return new UserRegistrationRequest
        {
            Username = "testuser123",
            Email = "test@example.com",
            Password = "TestPass123",
            ConfirmPassword = "TestPass123",
            BirthDate = new DateTime(1990, 1, 1),
            Age = 34,
            PhoneNumber = "0912345678",
            Roles = new List<string> { "User" },
            AgreeToTerms = true
        };
    }
}
```

### 角色驗證測試

```csharp
public class RoleValidationTests
{
    private readonly UserRegistrationValidator _validator;

    public RoleValidationTests()
    {
        var fakeTimeProvider = new FakeTimeProvider();
        fakeTimeProvider.SetUtcNow(new DateTime(2024, 1, 1));
        _validator = new UserRegistrationValidator(fakeTimeProvider);
    }

    [Fact]
    public void Validate_角色清單為null_應該產生驗證錯誤()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Roles = null!;

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Roles)
              .WithErrorMessage("至少需要指定一個角色");
    }

    [Fact]
    public void Validate_角色清單為空_應該產生驗證錯誤()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Roles = new List<string>();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Roles)
              .WithErrorMessage("至少需要指定一個角色");
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    [InlineData("Manager")]
    [InlineData("Support")]
    public void Validate_有效的角色_應該通過驗證(string role)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Roles = new List<string> { role };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Roles);
    }

    [Theory]
    [InlineData("InvalidRole")]
    [InlineData("SuperUser")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_無效的角色_應該產生驗證錯誤(string invalidRole)
    {
        // Arrange
        var request = CreateValidRequest();
        request.Roles = new List<string> { invalidRole };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Roles)
              .WithErrorMessage("包含無效的角色");
    }

    [Fact]
    public void Validate_多個有效角色_應該通過驗證()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Roles = new List<string> { "User", "Manager" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Roles);
    }

    [Fact]
    public void Validate_包含無效角色的混合清單_應該產生驗證錯誤()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Roles = new List<string> { "User", "InvalidRole", "Admin" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Roles)
              .WithErrorMessage("包含無效的角色");
    }

    [Fact]
    public void Validate_所有有效角色_應該通過驗證()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Roles = new List<string> { "User", "Admin", "Manager", "Support" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Roles);
    }

    private UserRegistrationRequest CreateValidRequest()
    {
        return new UserRegistrationRequest
        {
            Username = "testuser123",
            Email = "test@example.com",
            Password = "TestPass123",
            ConfirmPassword = "TestPass123",
            BirthDate = new DateTime(1990, 1, 1),
            Age = 34,
            PhoneNumber = "0912345678",
            Roles = new List<string> { "User" },
            AgreeToTerms = true
        };
    }
}
```

---

## 測試資料管理與最佳實務

### 寫個可重用的測試資料輔助方法

為了避免在每個測試中重複建立測試資料，我們可以寫個輔助方法：

```csharp
public class UserRegistrationValidatorTestsImproved
{
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly UserRegistrationValidator _validator;

    public UserRegistrationValidatorTestsImproved()
    {
        _fakeTimeProvider = new FakeTimeProvider();
        _fakeTimeProvider.SetUtcNow(new DateTime(2024, 1, 1));
        _validator = new UserRegistrationValidator(_fakeTimeProvider);
    }

    [Fact]
    public void Validate_完整的有效請求_應該通過驗證()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_多個無效欄位_應該回傳所有錯誤()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Username = ""; // 無效使用者名稱
        request.Email = "invalid"; // 無效電子郵件
        request.Password = "weak"; // 無效密碼
        request.AgreeToTerms = false; // 未同意條款

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Username);
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
        result.ShouldHaveValidationErrorFor(x => x.AgreeToTerms);
    }

    private UserRegistrationRequest CreateValidRequest()
    {
        return new UserRegistrationRequest
        {
            Username = "testuser123",
            Email = "test@example.com",
            Password = "TestPass123",
            ConfirmPassword = "TestPass123",
            BirthDate = new DateTime(1990, 1, 1),
            Age = 34,
            PhoneNumber = "0912345678",
            Roles = new List<string> { "User" },
            AgreeToTerms = true
        };
    }

    private UserRegistrationRequest CreateInvalidRequest()
    {
        return new UserRegistrationRequest
        {
            Username = "",
            Email = "invalid",
            Password = "weak",
            ConfirmPassword = "different",
            BirthDate = new DateTime(1990, 1, 1),
            Age = 25, // 不一致的年齡
            PhoneNumber = "123", // 無效格式
            Roles = new List<string>(), // 空角色
            AgreeToTerms = false
        };
    }
}
```

---

## 測試實務要點

### 核心原則

1. **單一責任**：每個測試只驗證一個特定的驗證規則
2. **描述性命名**：測試名稱清楚說明情境和預期結果
3. **3A 模式**：Arrange、Act、Assert 結構清晰
4. **資料隔離**：使用輔助方法管理測試資料
5. **時間控制**：使用 FakeTimeProvider 控制時間相關驗證

### 測試技巧

#### 1. 參數化測試

使用 `Theory` 和 `InlineData` 測試多種輸入組合：

```csharp
[Theory]
[InlineData("invalid_input", "expected_error_message")]
public void Validate_參數化測試_應該回傳對應錯誤(string input, string expectedError)
{
    // 測試寫法
}
```

#### 2. 邊界值測試

重點測試邊界條件：

```csharp
[Theory]
[InlineData(17, "年齡必須大於或等於 18 歲")] // 邊界下限 - 1
[InlineData(18, null)] // 邊界下限
[InlineData(120, null)] // 邊界上限
[InlineData(121, "年齡必須小於或等於 120 歲")] // 邊界上限 + 1
public void Validate_年齡邊界值_應該正確驗證(int age, string expectedError)
{
    // 測試寫法
}
```

#### 3. 非同步驗證測試

正確處理非同步驗證：

```csharp
[Fact]
public async Task ValidateAsync_外部服務驗證_應該正確處理()
{
    // 設定 Mock 行為
    _mockService.MethodAsync(Arg.Any<string>()).Returns(Task.FromResult(true));

    // 跑非同步驗證
    var result = await _validator.TestValidateAsync(request);

    // 驗證結果和 Mock 呼叫
    result.ShouldNotHaveValidationErrorFor(x => x.Property);
    await _mockService.Received(1).MethodAsync(Arg.Any<string>());
}
```

#### 4. 條件式驗證測試

測試 `When` 條件下的驗證邏輯：

```csharp
[Fact]
public void Validate_條件不滿足_應該跳過驗證()
{
    // Arrange
    var request = CreateValidRequest();
    request.OptionalField = null; // 觸發跳過條件

    // Act
    var result = _validator.TestValidate(request);

    // Assert
    result.ShouldNotHaveValidationErrorFor(x => x.OptionalField);
}
```

### 常見陷阱與解決方案

#### 1. 時間相關驗證

**問題**：使用 `DateTime.Now` 導致測試不穩定
**解決**：使用 `FakeTimeProvider` 控制時間

#### 2. 外部相依

**問題**：驗證器依賴外部服務
**解決**：用 NSubstitute 做 Mock 物件

#### 3. 複雜驗證邏輯

**問題**：多欄位互動驗證難以測試
**解決**：拆分驗證規則，分別測試每個條件

#### 4. 測試資料管理

**問題**：重複寫測試資料
**解決**：寫輔助方法統一管理

### 效能考量

1. **避免過度驗證**：只測試必要的驗證路徑
2. **Mock 設定最佳化**：重用 Mock 設定，避免重複寫
3. **測試資料最小化**：只寫測試需要的最小資料集
4. **並行測試**：讓測試可以同時跑

### 維護性建議

1. **定期更新測試**：當驗證規則變更時，同步更新測試
2. **文件化複雜規則**：為複雜的業務驗證規則加上註解
3. **測試涵蓋率監控**：讓新的驗證規則都有對應測試
4. **重構測試程式碼**：定期檢視和重構測試程式碼

用 FluentValidation Test Extensions 寫驗證測試，複雜的業務規則管得住。附帶的好處是這些測試本身就是業務規則的活文件——規則改了，測試會先告訴你。

## 延伸閱讀

### FluentValidation 核心資源

- [FluentValidation 官方文件](https://docs.fluentvalidation.net/)
- [FluentValidation GitHub 專案](https://github.com/FluentValidation/FluentValidation)
- [FluentValidation NuGet 套件](https://www.nuget.org/packages/FluentValidation/)（`FluentValidation.TestHelper` 命名空間隨主套件提供）
- [FluentValidation.AspNetCore 整合套件](https://www.nuget.org/packages/FluentValidation.AspNetCore/)（**已停止維護**，僅供舊專案參考；新專案改用核心套件手動驗證）

### 測試工具資源

- [Microsoft.Extensions.TimeProvider.Testing](https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing/)
- [NSubstitute 官方文件](https://nsubstitute.github.io/)
- [AwesomeAssertions GitHub 專案](https://github.com/AwesomeAssertions/AwesomeAssertions)
- [xUnit 官方文件](https://xunit.net/)

### .NET 驗證相關文章

- [ASP.NET Core Model Validation](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/validation)
- [Custom validation attributes in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/mvc/models/validation#custom-validation)
- [FluentValidation vs Data Annotations 比較分析](https://docs.fluentvalidation.net/en/latest/comparing-to-data-annotations.html)

明天我們將進入整合測試的領域，學習整合測試的基礎架構與應用情境。

範例程式碼：

- <https://github.com/kevintsengtw/30days-in-testing-net10/tree/main/samples/day18>

---

**這是「重啟挑戰：老派軟體工程師的測試修練」的第十八天。明天會介紹 Day 19 - 整合測試入門：基礎架構與應用情境。**
