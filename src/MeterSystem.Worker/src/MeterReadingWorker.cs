using System.Text;
using System.Text.Json;
using Dapper;
using MeterSystem.Shared.Models;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class MeterReadingWorker : BackgroundService
{
    private readonly ILogger<MeterReadingWorker> _logger;
    private readonly IConfiguration _configuration;

    public MeterReadingWorker(ILogger<MeterReadingWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost"
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync("meter-readings", durable: false, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var message = JsonSerializer.Deserialize<MeterData>(json);

            if (message is null)
            {
                _logger.LogWarning("Received null or unreadable message, skipping");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            try
            {
                await SaveReadingsAsync(message);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save readings for meter {MeterNumber}", message.MeterNumber);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync("meter-readings", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        _logger.LogInformation("Worker started, listening for messages");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task SaveReadingsAsync(MeterData message)
    {
        string connectionString = _configuration["PostgreSQL:ConnectionString"]!;

        await using var conn = new NpgsqlConnection(connectionString);

        long meterId = await conn.ExecuteScalarAsync<long>(
            """
            INSERT INTO meters (meter_number) VALUES (@MeterNumber)
            ON CONFLICT (meter_number) DO UPDATE SET meter_number = EXCLUDED.meter_number
            RETURNING meter_id
            """,
            new { message.MeterNumber });

        foreach ((DateTime timestamp, double value) in message.Readings)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO meter_readings (meter_id, value_at, value)
                VALUES (@MeterId, @ValueAt, @Value)
                ON CONFLICT DO NOTHING
                """,
                new { MeterId = meterId, ValueAt = timestamp, Value = value });
        }

        _logger.LogInformation("Saved {Count} readings for meter {MeterNumber}", message.Readings.Count, message.MeterNumber);
    }
}
