using System.Text.Json.Serialization;

public class RawMeterRequest
{
    [JsonPropertyName("meter_number")]
    public long MeterNumber { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}
