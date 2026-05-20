using MeterSystem.Api.Proto;
using Microsoft.AspNetCore.Mvc;
using SharedMeterData = MeterSystem.Shared.Models.MeterData;

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
    public async Task<IActionResult> PostReadings([FromBody] SharedMeterData dto)
    {
        _logger.LogInformation("Received request for meter {MeterNumber}", dto.MeterNumber);

        if (dto.MeterNumber <= 0 || dto.Readings is null || dto.Readings.Count == 0)
        {
            _logger.LogError("Invalid request: MeterNumber={MeterNumber}, ReadingsCount={ReadingsCount}",
                dto.MeterNumber, dto.Readings?.Count);
            return BadRequest();
        }

        bool published = await MeterLogic.AddToMessageQueue(dto, _logger, _configuration);
        if (!published)
        {
            _logger.LogError("Failed to publish message for meter {MeterNumber}", dto.MeterNumber);
            return StatusCode(500);
        }

        return Accepted();
    }

    [HttpPost("readings/raw")]
    public async Task<IActionResult> PostRawReadings([FromBody] RawMeterRequest dto)
    {
        _logger.LogInformation("Received raw request for meter {MeterNumber}", dto.MeterNumber);

        if (dto.MeterNumber <= 0 || string.IsNullOrEmpty(dto.Data))
        {
            _logger.LogError("Invalid raw request: MeterNumber={MeterNumber}, DataLength={DataLength}",
                dto.MeterNumber, string.IsNullOrEmpty(dto.Data) ? 0 : dto.Data.Length);
            return BadRequest();
        }

        MeterData protoData;
        try
        {
            byte[] bytes = Convert.FromBase64String(dto.Data);
            protoData = MeterData.Parser.ParseFrom(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse protobuf data for meter {MeterNumber}", dto.MeterNumber);
            return BadRequest();
        }

        if (protoData.Readings.Count == 0)
        {
            _logger.LogError("Protobuf data contains no readings for meter {MeterNumber}", dto.MeterNumber);
            return BadRequest();
        }

        var message = new SharedMeterData(
            dto.MeterNumber,
            protoData.Readings.ToDictionary(
                r => r.Timestamp.ToDateTime(),
                r => r.Value));

        bool published = await MeterLogic.AddToMessageQueue(message, _logger, _configuration);
        if (!published)
        {
            _logger.LogError("Failed to publish message for meter {MeterNumber}", dto.MeterNumber);
            return StatusCode(500);
        }

        return Accepted();
    }
}
