using Prometheus;
using System.Diagnostics.Metrics;

namespace AgroSolutions.AlertProcessor.Metrics;

public static class AppMetrics
{
    public static readonly Counter MeasurementsSaved =
        Prometheus.Metrics.CreateCounter("measurements_saved_total", "Total de medições salvas no MongoDB");

    public static readonly Counter AlertsCreated =
        Prometheus.Metrics.CreateCounter("alerts_created_total", "Total de alertas criados");

    public static readonly Gauge AlertsActive =
        Prometheus.Metrics.CreateGauge("alerts_active_total", "Total de alertas ativos (gauge)");

    public static readonly Counter ProcessingErrors =
        Prometheus.Metrics.CreateCounter("processing_errors_total", "Total de erros ao processar mensagens");

    public static readonly Histogram ProcessingDurationMs =
        Prometheus.Metrics.CreateHistogram("message_processing_duration_ms", "Tempo para processar 1 mensagem (ms)");
}