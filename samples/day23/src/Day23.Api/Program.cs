using Day23.Api.Middleware;
using Day23.Application.Abstractions;
using Day23.Application.Services;
using Day23.Application.Validation;
using Day23.Infrastructure.Caching;
using Day23.Infrastructure.Database;
using Day23.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 關閉 [ApiController] 的自動 ModelState 400 短路，讓所有輸入驗證都走
// service 層的 FluentValidation → ValidationException → FluentValidationExceptionHandler，
// 例外處理路徑才會單一且可被整合測試驗證。
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// Add FluentValidation（核心套件 + DI 擴充）
// 只把 validator 註冊進 DI，供 service 層以 ValidateAndThrowAsync 明確驗證；
// 不使用已停止維護的 FluentValidation.AspNetCore auto-validation。
builder.Services.AddValidatorsFromAssemblyContaining<ProductCreateValidator>();

// Add ProblemDetails
builder.Services.AddProblemDetails();

// Add Exception Handler
builder.Services.AddExceptionHandler<FluentValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Add TimeProvider
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// Add Infrastructure services
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddSingleton<ICacheService, RedisCacheService>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// Add Application services
builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Use Exception Handler
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// 為測試專案提供程式進入點的存取
public partial class Program
{
}