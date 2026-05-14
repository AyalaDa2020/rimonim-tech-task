using System.Text.Json.Serialization;

public class MeterRequest
{
    public MeterRequest(long meterNumber, Dictionary<string, double>? readings)
    {
        MeterNumber = meterNumber;
        Readings = readings;
    }

    [JsonPropertyName("meter_number")]
    public long MeterNumber { get; }

    [JsonPropertyName("readings")]
    public Dictionary<string, double>? Readings { get; }
}
