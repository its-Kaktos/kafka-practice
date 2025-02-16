using System.Globalization;
using System.Text.Json;
using Confluent.Kafka;

namespace Producer;

public sealed class ProduceBackgroundService : BackgroundService
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<ProduceBackgroundService> _logger;

    public ProduceBackgroundService(ILogger<ProduceBackgroundService> logger)
    {
        _logger = logger;

        var config = new ProducerConfig()
        {
            Acks = Acks.All,
            BootstrapServers = "localhost:9092",
            RetryBackoffMs = 100,
            RetryBackoffMaxMs = 1_000,
            MessageSendMaxRetries = 10,
            EnableIdempotence = true,
            StatisticsIntervalMs = 10_000,
            // Debug = "broker,topic,msg"
        };

        _producer = new ProducerBuilder<Null, string>(config)
            .SetErrorHandler((_, error) => _logger.LogError("Kafka producer exception occured, exception is {Exception}", JsonSerializer.Serialize(error)))
            .SetStatisticsHandler((_, s) => _logger.LogInformation(s))
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var x = await _producer.ProduceAsync("practice.kafka.background_service", new Message<Null, string>()
            {
                // Key = new Null(),
                Headers = null,
                Timestamp = new Timestamp(DateTime.Now, TimestampType.CreateTime),
                Value = "hello this message was created at " + DateTime.Now
            }, stoppingToken);

            _logger.LogInformation("produced single event to Kafka, time {Time}", DateTime.Now.ToString("O", CultureInfo.CurrentCulture));

            // await Task.Delay(1_000, stoppingToken);
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        _producer.Flush();
        _producer.Dispose();
    }
}