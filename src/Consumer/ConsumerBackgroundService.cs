using System.Text.Json;
using Confluent.Kafka;

namespace Consumer;

public sealed class ConsumerBackgroundService : BackgroundService
{
    private readonly IConsumer<Null, string> _consumer;
    private readonly ILogger<ConsumerBackgroundService> _logger;

    public ConsumerBackgroundService(ILogger<ConsumerBackgroundService> logger)
    {
        _logger = logger;
        var conf = new ConsumerConfig()
        {
            GroupId = "practice-kafka-consumer-group",
            BootstrapServers = "localhost:9092",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            StatisticsIntervalMs = 10_000,
            // Debug = "broker,topic,msg"
        };

        _consumer = new ConsumerBuilder<Null, string>(conf)
            // .SetLogHandler((_, message) => _logger.LogInformation(JsonSerializer.Serialize(message)))
            .SetErrorHandler((_, error) => _logger.LogError("Error occured in Kafka consumer, error is {Error}", JsonSerializer.Serialize(error)))
            .SetStatisticsHandler((_, s) => _logger.LogInformation(s))
            .Build();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ExecuteInternalAsync(stoppingToken), stoppingToken);
    }

    private Task ExecuteInternalAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("practice.kafka.background_service");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = _consumer.Consume(stoppingToken);

            _logger.LogInformation("Consumed result: {Result}", JsonSerializer.Serialize(result));
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();

        _consumer.Close();
        _consumer.Dispose();
    }
}