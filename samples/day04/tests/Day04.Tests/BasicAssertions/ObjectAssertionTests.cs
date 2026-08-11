using Day04.Domain.Models;
using Day04.Domain.Services;

namespace Day04.Tests.BasicAssertions;

/// <summary>
/// class ObjectAssertionTests - 物件斷言測試
/// </summary>
public class ObjectAssertionTests
{
    private readonly UserService _userService = new();

    [Fact]
    public void NotBeNull_BeOfType_物件基本檢查_應驗證類型和非空值()
    {
        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };
        User? nullUser = null;

        // 基本物件斷言
        user.Should().NotBeNull();
        user.Should().BeOfType<User>();
        user.Should().BeAssignableTo<IUser>();

        // 空值斷言
        nullUser.Should().BeNull();

        // 相等性斷言
        var anotherUser = new User { Id = 1, Name = "John", Email = "john@example.com" };
        user.Should().BeEquivalentTo(anotherUser,
                                     options => options.Excluding(u => u.CreatedAt),
                                     "因為除了時間戳記外所有屬性都應該相同");
    }

    [Fact]
    public void BeEquivalentTo_物件屬性比較_應匹配所有屬性值()
    {
        var user = new User { Id = 1, Name = "John", Email = "john@example.com" };

        // 屬性值斷言
        user.Id.Should().Be(1);
        user.Name.Should().NotBeNullOrEmpty()
            .And.StartWith("J")
            .And.HaveLength(4);
        user.Email.Should().Contain("@")
            .And.EndWith(".com");
    }

    [Fact]
    public void Should_AwesomeAssertions基本功能_應正常運作()
    {
        const string result = "Hello World";

        // 使用 AwesomeAssertions 的流暢語法
        result.Should().NotBeNull()
              .And.StartWith("Hello")
              .And.EndWith("World")
              .And.HaveLength(11);
    }
}
