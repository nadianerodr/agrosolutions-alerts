using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace AgroSolutions.AlertProcessor.Models;

public class Alert
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PlotId { get; set; } = default!;
    public string Type { get; set; } = default!; 
    public string Message { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTime TriggeredAtUtc { get; set; } = DateTime.UtcNow;
}