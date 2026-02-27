using AgroSolutions.AlertProcessor.Metrics;
using AgroSolutions.AlertProcessor.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AgroSolutions.AlertProcessor.Services;

public class AlertProcessorService
{
    private readonly IMongoCollection<SensorMeasurement> _measurements;
    private readonly IMongoCollection<Alert> _alerts;
    private readonly ILogger<AlertProcessorService> _logger;

    private readonly double _humidityThreshold;
    private readonly int _windowHours;

    public AlertProcessorService(IMongoDatabase db, IConfiguration config, ILogger<AlertProcessorService> logger)
    {
        _logger = logger;

        _measurements = db.GetCollection<SensorMeasurement>("Measurements");
        _alerts = db.GetCollection<Alert>("Alerts");

        _humidityThreshold = config.GetValue<double>("AlertRules:DroughtHumidityThreshold");
        _windowHours = config.GetValue<int>("AlertRules:DroughtWindowHours");
    }

    public async Task ProcessAsync(SensorMeasurement m, CancellationToken ct)
    {
        try
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (string.IsNullOrWhiteSpace(m.PlotId)) throw new ArgumentException("PlotId is required");

            await _measurements.InsertOneAsync(m, cancellationToken: ct);
            AppMetrics.MeasurementsSaved.Inc();

            _logger.LogInformation("Saved measurement PlotId={PlotId} Humidity={Humidity} Timestamp={Timestamp}",
                m.PlotId, m.Humidity, m.Timestamp);

            await TryCreateDroughtAlertAsync(m.PlotId, ct);

            var activeCount = await _alerts.CountDocumentsAsync(a => a.IsActive, cancellationToken: ct);
            AppMetrics.AlertsActive.Set(activeCount);
        }
        catch (MongoException ex)
        {
            AppMetrics.ProcessingErrors.Inc();
            _logger.LogError(ex, "MongoDB error processing PlotId={PlotId}", m?.PlotId);
            throw;
        }
        catch (Exception ex)
        {
            AppMetrics.ProcessingErrors.Inc();
            _logger.LogError(ex, "Unexpected error processing PlotId={PlotId}", m?.PlotId);
            throw;
        }
    }

    private async Task TryCreateDroughtAlertAsync(string plotId, CancellationToken ct)
    {
        var now = DateTime.Now;
        var from = now.AddHours(-_windowHours);

        var existingActive = await _alerts
            .Find(a => a.PlotId == plotId && a.Type == "DROUGHT" && a.IsActive)
            .FirstOrDefaultAsync(ct);

        if (existingActive != null)
            return;

        var windowMeasurements = await _measurements
            .Find(x => x.PlotId == plotId && x.Timestamp >= from && x.Timestamp <= now)
            .SortBy(x => x.Timestamp)
            .ToListAsync(ct);

        if (windowMeasurements.Count == 0)
            return;

        // precisa existir medição próxima do início da janela para considerar “por 24h”
        var oldest = windowMeasurements.First().Timestamp;
        if (oldest > from.AddMinutes(5))
            return;

        var allBelow = windowMeasurements.All(x => x.Humidity < _humidityThreshold);
        if (!allBelow)
            return;

        var alert = new Alert
        {
            PlotId = plotId,
            Type = "DROUGHT",
            Message = $"Alerta de seca: umidade < {_humidityThreshold}% por {_windowHours}h",
            IsActive = true,
            TriggeredAtUtc = now
        };

        await _alerts.InsertOneAsync(alert, cancellationToken: ct);
        AppMetrics.AlertsCreated.Inc();

        _logger.LogWarning("DROUGHT ALERT triggered for PlotId={PlotId}", plotId);
    }
}