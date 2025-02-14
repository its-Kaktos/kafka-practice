using System.Globalization;
using Confluent.Kafka;

namespace Producer;

public class ProduceBackgroundService : BackgroundService
{
    private readonly ProducerConfig _config = new()
    {
        Acks = Acks.All,
        BootstrapServers = "localhost:9092",
        RetryBackoffMs = 100,
        RetryBackoffMaxMs = 1_000,
        MessageSendMaxRetries = 10,
        EnableIdempotence = true
    };

    private readonly ILogger<ProduceBackgroundService> _logger;

    public ProduceBackgroundService(ILogger<ProduceBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var p = new ProducerBuilder<Null, string>(_config).Build();

            var x = await p.ProduceAsync("practice.kafka.background_service", new Message<Null, string>()
            {
                // Key = new Null(),
                Headers = null,
                Timestamp = new Timestamp(DateTime.Now, TimestampType.CreateTime),
                Value = "hello this message was created at " + DateTime.Now
            }, stoppingToken);

            _logger.LogInformation("produced single event to Kafka, time {Time}", DateTime.Now.ToString("O",CultureInfo.CurrentCulture));

            // await Task.Delay(5_000, stoppingToken);
        }
    }
}