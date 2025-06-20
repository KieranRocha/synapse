// Models/ActivityData.cs
namespace CADCompanion.Agent.Models
{
    public class ActivityData
    {
        public string Type { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? MachineId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string User { get; set; } = string.Empty;
        public string CompanionId { get; set; } = string.Empty;
    }

    public class PartDataWithContext
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string PartFileName { get; set; } = string.Empty;
        public string PartFilePath { get; set; } = string.Empty;
        public DateTime ExtractedAt { get; set; }
        public string ExtractedBy { get; set; } = Environment.MachineName;
        public string? WorkSessionId { get; set; }
        public string? Engineer { get; set; }
        public PartProperties? Properties { get; set; }
        public string InventorVersion { get; set; } = string.Empty;
    }

    public class PartProperties
    {
        public string PartNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Material { get; set; }
        public double Mass { get; set; }
        public double Volume { get; set; }
        public double Area { get; set; }
        public string? StockNumber { get; set; }
        public string? Vendor { get; set; }
        public decimal? Cost { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }
}