using Day03.Tests.TestDataProviders;

namespace Day03.Tests.BuilderPatternTests;

/// <summary>
/// class UserValidationTests - 用於測試使用者驗證的功能。
/// </summary>
public class UserValidationTests
{
    private readonly ITestDataProvider<User> _userDataProvider;
    private readonly UserValidator _validator;

    /// <summary>
    /// UserValidationTests 的建構函式
    /// </summary>
    public UserValidationTests()
    {
        // 初始化使用者資料提供者和驗證器
        this._userDataProvider = new UserTestDataProvider();
        this._validator = new UserValidator();
    }

    [Theory]
    [MemberData(nameof(GetValidUsers))]
    public void ValidateUser_有效使用者_應通過驗證(User user)
    {
        // Act
        var result = this._validator.IsValid(user);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(GetInvalidUsers))]
    public void ValidateUser_無效使用者_應驗證失敗(User user)
    {
        // Act
        var result = this._validator.IsValid(user);

        // Assert
        Assert.False(result);
    }

    public static IEnumerable<object[]> GetValidUsers()
    {
        var provider = new UserTestDataProvider();
        return provider.GetValidData().Select(user => new object[] { user });
    }

    public static IEnumerable<object[]> GetInvalidUsers()
    {
        var provider = new UserTestDataProvider();
        return provider.GetInvalidData().Select(user => new object[] { user });
    }
}