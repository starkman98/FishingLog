using System.ComponentModel.DataAnnotations;

namespace FishingLog.Domain.Entities;

public class FishingTrip
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Name { get; set; } = string.Empty;
    public string? LocationName { get; set; } = string.Empty;
    public double? WaterTemp { get; set; }
    public string? WeatherDescription { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
