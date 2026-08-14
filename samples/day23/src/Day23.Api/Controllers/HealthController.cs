namespace Day23.Api.Controllers;

/// <summary>
/// 健康檢查控制器
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly TimeProvider _timeProvider;

    public HealthController(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 健康檢查端點
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        var response = new
        {
            status = "ok",
            timestamp = _timeProvider.GetUtcNow(),
            version = "1.0.0"
        };

        return Ok(response);
    }
}