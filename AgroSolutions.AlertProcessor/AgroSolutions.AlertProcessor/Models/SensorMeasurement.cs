using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace AgroSolutions.AlertProcessor.Models;

public class SensorMeasurement
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PlotId { get; set; } = default!;
    public double Humidity { get; set; }
    public double Temperature { get; set; }
    public double Rainfall { get; set; }
    public DateTime Timestamp { get; set; } 

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}