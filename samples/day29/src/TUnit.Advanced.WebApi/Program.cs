using TUnit.Advanced.Core.Models;
using TUnit.Advanced.Core.Services;
using TUnit.Advanced.WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddSingleton<IDiscountRepository, EmptyDiscountRepository>();
builder.Services.AddSingleton<IDiscountCalculator, DiscountCalculator>();
builder.Services.AddSingleton<IShippingCalculator, ShippingCalculator>();
builder.Services.AddSingleton<IOrderService, OrderService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
   {
       var forecast = Enumerable.Range(1, 5)
                                .Select(index => new WeatherForecast(
                                            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                                            Random.Shared.Next(-20, 55),
                                            summaries[Random.Shared.Next(summaries.Length)]
                                        ))
                                .ToArray();
       return forecast;
   })
   .WithName("GetWeatherForecast");

app.MapPost("/orders", CreateOrderAsync)
   .WithName("CreateOrder")
   .Produces<Order>(StatusCodes.Status201Created)
   .ProducesValidationProblem();

app.MapGet("/orders/{orderId}", GetOrderAsync)
   .WithName("GetOrder")
   .Produces<Order>()
   .ProducesProblem(StatusCodes.Status404NotFound);

app.Run();

static async Task<IResult> CreateOrderAsync(CreateOrderRequest request, IOrderService orderService)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.CustomerId))
    {
        errors[nameof(request.CustomerId)] = ["CustomerId is required."];
    }

    if (request.Items is null || request.Items.Count == 0)
    {
        errors[nameof(request.Items)] = ["At least one order item is required."];
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var items = request.Items!
                       .Select(item => new OrderItem
                       {
                           ProductId = item.ProductId,
                           ProductName = item.ProductName,
                           UnitPrice = item.UnitPrice,
                           Quantity = item.Quantity
                       })
                       .ToList();

    try
    {
        var order = await orderService.CreateOrderAsync(
            request.CustomerId!,
            request.CustomerLevel,
            items);

        return Results.Created($"/orders/{order.OrderId}", order);
    }
    catch (ArgumentException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Order"] = [exception.Message]
        });
    }
}

static async Task<IResult> GetOrderAsync(string orderId, IOrderService orderService)
{
    var order = await orderService.GetOrderByIdAsync(orderId);
    if (order is null)
    {
        return Results.Problem(
            title: "Order not found",
            detail: $"Order '{orderId}' does not exist.",
            statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Ok(order);
}

// 讓整合測試能夠存取 Program 類別
public partial class Program
{
}

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public sealed record CreateOrderRequest(
    string? CustomerId,
    CustomerLevel CustomerLevel,
    IReadOnlyList<CreateOrderItemRequest>? Items);

public sealed record CreateOrderItemRequest(
    string ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity);
