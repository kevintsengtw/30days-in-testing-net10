namespace ValidationExample.Tests.Validators;

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