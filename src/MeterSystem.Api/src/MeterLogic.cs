using System.Text;
using System.Text.Json;
using MeterSystem.Shared.Models;
using RabbitMQ.Client;

public class MeterLogic
{
    public static async Task<bool> AddToMessageQueue(MeterData message, ILogger logger, IConfiguration configuration)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"] ?? "localhost"
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync("meter-readings", durable: false, exclusive: false, autoDelete: false);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "meter-readings",
                body: body);

            logger.LogInformation("Published meter reading to RabbitMQ for meter {MeterNumber}", message.MeterNumber);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish meter reading to RabbitMQ for meter {MeterNumber}", message.MeterNumber);
            return false;
        }
    }
}
