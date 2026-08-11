using Day06.Tests.Extensions;

namespace Day06.Tests.CustomAssertionTests;

// 使用自訂Assertions
public class ProductServiceTests
{
    private readonly ProductService _productService;

    public ProductServiceTests()
    {
        this._productService = new ProductService();
    }

    [Fact]
    public void CreateProduct_有效資料_應該回傳有效產品()
    {
        var actual = this._productService.Create("Laptop", 999.99m);

        // 使用領域特定Assertions
        actual.Should().BeValidProduct();
        actual.Name.Should().Be("Laptop");
    }
}

public class OrderServiceTests
{
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        this._orderService = new OrderService();
    }

    [Fact]
    public void ProcessOrder_有效項目_應該回傳有效訂單()
    {
        // Arrange
        var items = new[]
        {
            new OrderItem { ProductId = 1, Quantity = 2, Price = 50.0m },
            new OrderItem { ProductId = 2, Quantity = 1, Price = 100.0m }
        };

        // Act
        var actual = this._orderService.CreateOrder(items);

        // Assert
        // 使用領域特定Assertions
        actual.Should().BeValidOrder();
        actual.Items.Should().HaveCount(2);
    }
}

// 使用條件式Assertions
public class ConditionalAssertionTests
{
    private readonly UserService _userService;

    public ConditionalAssertionTests()
    {
        this._userService = new UserService();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProcessUser_依據角色_應有正確權限(bool isAdmin)
    {
        // Act
        var actual = this._userService.ProcessUser(isAdmin);

        actual.Should().NotBeNull();
        if (isAdmin)
        {
            actual.Role.Should().Be("admin");
            actual.Permissions.Should().Contain("DELETE");
        }
        else
        {
            actual.Role.Should().Be("user");
            actual.Permissions.Should().NotContain("DELETE");
        }
    }
}
