using System.Text.Json.Serialization;

namespace MeterSystem.Shared.Models;

public record MeterData(
    [property: JsonPropertyName("meter_number")] long MeterNumber,
    [property: JsonPropertyName("readings")] Dictionary<DateTime, double> Readings
);
