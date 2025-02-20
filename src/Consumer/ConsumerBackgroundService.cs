using System.Text.Json;
using Confluent.Kafka;
using ILogger = Serilog.ILogger;

namespace Consumer;

public sealed class ConsumerBackgroundService : BackgroundService
{
    private readonly IConsumer<Null, string> _consumer;
    private readonly ILogger _logger;

    public ConsumerBackgroundService(ILogger logger)
    {
        _logger = logger;
        var conf = new ConsumerConfig()
        {
            GroupId = "practice-kafka-consumer-group",
            BootstrapServers = "localhost:9092",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            StatisticsIntervalMs = 10_000,
            // Debug = "broker,topic,msg",
        };

        _consumer = new ConsumerBuilder<Null, string>(conf)
            // .SetLogHandler((_, message) => _logger.LogInformation(JsonSerializer.Serialize(message)))
            .SetErrorHandler((_, error) => _logger.Error("Error occured in Kafka consumer, error is {@Error}", error))
            .SetStatisticsHandler((_, s) => _logger.Information("Statics logs from Kafka {@Statistics}", JsonSerializer.Deserialize<KafkaStatisticsDto>(s)))
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

            _logger.Information("Consumed result: {@Result}", result);
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