using System.Text.Json;
using Confluent.Kafka;
using Producer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHostedService<ProduceBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();

app.MapPost("/produce-single", async (ILogger<Program> logger) =>
    {
        var config = new ProducerConfig()
        {
            Acks = Acks.Leader,
            BootstrapServers = "localhost:9092"
        };

        using var p = new ProducerBuilder<Null, string>(config).Build();

        var x = await p.ProduceAsync("topic", new Message<Null, string>()
        {
            // Key = new Null(),
            Headers = null,
            Timestamp = new Timestamp(DateTime.Now, TimestampType.CreateTime),
            Value = "hello this message was created at " + DateTime.Now
        });

        logger.LogInformation("produced single event to Kafka {Result}", JsonSerializer.Serialize(x));

        return Results.Ok(x);
    })
    .WithName("ProduceSingle");

await app.RunAsync();