using MeterSystem.Shared.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public class MeterController : ControllerBase
{
    private readonly ILogger<MeterController> _logger;
    private readonly IConfiguration _configuration;

    public MeterController(ILogger<MeterController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("readings")]
    public async Task<IActionResult> PostReadings([FromBody] MeterData dto)
    {
        _logger.LogInformation("Received request for meter {MeterNumber}", dto.MeterNumber);

        if (dto.MeterNumber <= 0 || dto.Readings is null || dto.Readings.Count == 0)
        {
            _logger.LogWarning("Invalid request: MeterNumber={MeterNumber}, ReadingsCount={ReadingsCount}",
                dto.MeterNumber, dto.Readings?.Count);
            return BadRequest();
        }

        bool published = await MeterLogic.AddToMessageQueue(dto, _logger, _configuration);
        if (!published)
        {
            return StatusCode(500);
        }

        return Accepted();
    }
}
