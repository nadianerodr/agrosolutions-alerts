using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using AgroSolutions.AlertProcessor.Metrics;
using AgroSolutions.AlertProcessor.Models;
using AgroSolutions.AlertProcessor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace AgroSolutions.AlertProcessor.Workers;

public class SqsConsumerWorker : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly AlertProcessorService _processor;
    private readonly ILogger<SqsConsumerWorker> _logger;
    private readonly string _queueUrl;

    public SqsConsumerWorker(IAmazonSQS sqs, AlertProcessorService processor, IConfiguration config, ILogger<SqsConsumerWorker> logger)
    {
        _sqs = sqs;
        _processor = processor;
        _logger = logger;
        _queueUrl = config["Aws:QueueUrl"]!;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SQS Consumer started. QueueUrl={QueueUrl}", _queueUrl);

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;

            try
            {
                response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 10,
                    VisibilityTimeout = 30
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                AppMetrics.ProcessingErrors.Inc();
                _logger.LogError(ex, "Error receiving messages");
                await Task.Delay(2000, stoppingToken);
                continue;
            }

            foreach (var msg in response.Messages)
            {
                using var timer = AppMetrics.ProcessingDurationMs.NewTimer();

                try
                {
                    SensorMeasurement? measurement;
                    try
                    {
                        measurement = JsonSerializer.Deserialize<SensorMeasurement>(msg.Body, jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        AppMetrics.ProcessingErrors.Inc();
                        _logger.LogError(ex, "Invalid JSON body: {Body}", msg.Body);
                        continue; 
                    }

                    if (measurement == null || string.IsNullOrWhiteSpace(measurement.PlotId))
                        throw new Exception("Invalid measurement (null or PlotId missing)");

                    await _processor.ProcessAsync(measurement, stoppingToken);

                    await _sqs.DeleteMessageAsync(_queueUrl, msg.ReceiptHandle, stoppingToken);
                    _logger.LogInformation("Message processed and deleted. MessageId={MessageId}", msg.MessageId);
                }
                catch (Exception ex)
                {
                    AppMetrics.ProcessingErrors.Inc();
                    _logger.LogError(ex, "Failed to process message. MessageId={MessageId}", msg.MessageId);
                }
            }
        }
    }
}