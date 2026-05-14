using System.Globalization;
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
    public async Task<IActionResult> PostReadings([FromBody] MeterRequest dto)
    {
        _logger.LogInformation("Received request for meter {MeterNumber}", dto.MeterNumber);

        if (!IsValidRequest(dto))
        {
            return BadRequest();
        }

        bool published = await MeterLogic.AddToMessageQueue(dto, _logger, _configuration);
        if (!published)
        {
            return StatusCode(500);
        }

        return Accepted();
    }

    private bool IsValidRequest(MeterRequest dto)
    {
        if (dto.MeterNumber <= 0 || dto.Readings is null || dto.Readings.Count == 0)
        {
            _logger.LogWarning("Invalid request: MeterNumber={MeterNumber}, ReadingsCount={ReadingsCount}",
                dto.MeterNumber, dto.Readings?.Count);
            return false;
        }

        foreach (string timestamp in dto.Readings.Keys)
        {
            if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _))
            {
                _logger.LogWarning("Invalid timestamp format: {Timestamp}", timestamp);
                return false;
            }
        }

        return true;
    }
}
