using Amazon.SQS;
using AgroSolutions.AlertProcessor.Services;
using AgroSolutions.AlertProcessor.Workers;
using MongoDB.Driver;
using Prometheus;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Mongo
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(cfg["Mongo:ConnectionString"])
);

builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(cfg["Mongo:Database"]);
});

// SQS (LocalStack)
builder.Services.AddSingleton<IAmazonSQS>(_ =>
{
    var serviceUrl = cfg["Aws:ServiceUrl"];
    var region = cfg["Aws:Region"];

    var sqsConfig = new AmazonSQSConfig
    {
        ServiceURL = serviceUrl,
        AuthenticationRegion = region
    };

    return new AmazonSQSClient("test", "test", sqsConfig);
});

builder.Services.AddSingleton<AlertProcessorService>();
builder.Services.AddHostedService<SqsConsumerWorker>();

var app = builder.Build();

// Prometheus endpoint
app.UseRouting();
app.UseHttpMetrics();     
app.MapMetrics("/metrics");

app.Run("http://0.0.0.0:9102");