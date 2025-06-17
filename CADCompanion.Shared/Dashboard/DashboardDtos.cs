// CADCompanion.Shared/Dashboard/DashboardDtos.cs
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Shared.Dashboard
{
    // ================================
    // DASHBOARD KPIs
    // ================================

    public class DashboardKPIsDto
    {
        public KPIValueDto ActiveProjects { get; set; } = new();
        public KPIValueDto TotalEngineers { get; set; } = new();
        public KPIValueDto BomVersions { get; set; } = new();
        public KPIValueDto SystemHealth { get; set; } = new();
    }

    public class KPIValueDto
    {
        public int Value { get; set; }
        public int Change { get; set; }
        public string Trend { get; set; } = "up"; // "up" or "down"
    }

    // ================================
    // SYSTEM ALERTS
    // ================================

    public class AlertDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = "info"; // "error", "warning", "info"
        public string Message { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string Severity { get; set; } = "low"; // "low", "medium", "high", "critical"
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public string? ActionUrl { get; set; }
    }

    // ================================
    // PROJECT ACTIVITY
    // ================================

    public class ProjectActivityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Activity { get; set; } // Progress percentage
        public string Status { get; set; } = "planning";
        public string Deadline { get; set; } = string.Empty;
        public int Budget { get; set; } // Budget usage percentage
        public string? LastActivity { get; set; }
        public string? ResponsibleEngineer { get; set; }
        public int TotalBomVersions { get; set; }
        public DateTime? LastBomExtraction { get; set; }
    }

    // ================================
    // BOM STATISTICS
    // ================================

    public class BOMStatsDto
    {
        public int TotalExtractions { get; set; }
        public double SuccessRate { get; set; }
        public double AvgProcessingTime { get; set; }
        public int LastHour { get; set; }
        public int FailedExtractions { get; set; }
        public double SystemAvailability { get; set; }
        public List<BOMExtractionTrendDto> HourlyTrend { get; set; } = new();
    }

    public class BOMExtractionTrendDto
    {
        public DateTime Hour { get; set; }
        public int Extractions { get; set; }
        public int Failures { get; set; }
        public double AvgTime { get; set; }
    }

    // ================================
    // ENGINEER ACTIVITY
    // ================================

    public class EngineerActivityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Projects { get; set; }
        public int Saves { get; set; }
        public double Hours { get; set; }
        public string Status { get; set; } = "offline"; // "online", "away", "offline"
        public string LastActivity { get; set; } = string.Empty;
        public string? CurrentProject { get; set; }
        public string? WorkstationId { get; set; }
        public List<EngineerProjectDto> ActiveProjects { get; set; } = new();
    }

    public class EngineerProjectDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public TimeSpan TimeSpent { get; set; }
        public int SaveCount { get; set; }
        public DateTime LastActivity { get; set; }
    }

    // ================================
    // DASHBOARD FILTERS
    // ================================

    public class DashboardFiltersDto
    {
        [Required]
        public string TimeRange { get; set; } = "24h"; // "1h", "24h", "7d", "30d"
        public string? ProjectFilter { get; set; }
        public string? EngineerFilter { get; set; }
        public string? StatusFilter { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    // ================================
    // SYSTEM STATUS
    // ================================

    public class SystemStatusDto
    {
        public string Status { get; set; } = "healthy"; // "healthy", "warning", "critical"
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public int ActiveConnections { get; set; }
        public TimeSpan Uptime { get; set; }
        public List<ServiceStatusDto> Services { get; set; } = new();
        public DateTime LastHealthCheck { get; set; }
    }

    public class ServiceStatusDto
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "unknown";
        public string? ErrorMessage { get; set; }
        public DateTime LastCheck { get; set; }
        public TimeSpan ResponseTime { get; set; }
    }

    // ================================
    // REAL-TIME UPDATES
    // ================================

    public class DashboardUpdateDto
    {
        public string Type { get; set; } = string.Empty; // "kpi", "alert", "activity", "bom"
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? TargetUsers { get; set; } // Specific users or "all"
    }

    // ================================
    // ANALYTICS REQUESTS
    // ================================

    public class AnalyticsRequestDto
    {
        [Required]
        public string MetricType { get; set; } = string.Empty; // "productivity", "quality", "efficiency"
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }
        public List<string> ProjectIds { get; set; } = new();
        public List<string> EngineerIds { get; set; } = new();
        public string GroupBy { get; set; } = "day"; // "hour", "day", "week", "month"
    }

    public class AnalyticsResponseDto
    {
        public string MetricType { get; set; } = string.Empty;
        public List<AnalyticsDataPointDto> DataPoints { get; set; } = new();
        public Dictionary<string, object> Summary { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class AnalyticsDataPointDto
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Label { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    // ================================
    // EXPORT REQUESTS
    // ================================

    public class ExportRequestDto
    {
        [Required]
        public string Type { get; set; } = string.Empty; // "dashboard", "projects", "bom", "analytics"
        [Required]
        public string Format { get; set; } = "excel"; // "excel", "pdf", "csv"
        public DashboardFiltersDto? Filters { get; set; }
        public List<string> IncludedSections { get; set; } = new();
        public string? FileName { get; set; }
    }

    public class ExportResponseDto
    {
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Format { get; set; } = string.Empty;
    }

    // ================================
    // NOTIFICATION SETTINGS
    // ================================

    public class NotificationSettingsDto
    {
        public bool EnableRealTime { get; set; } = true;
        public bool EnableEmail { get; set; } = false;
        public List<string> AlertTypes { get; set; } = new(); // Which alert types to receive
        public List<string> ProjectIds { get; set; } = new(); // Which projects to monitor
        public string EmailAddress { get; set; } = string.Empty;
        public int QuietHoursStart { get; set; } = 18; // 6 PM
        public int QuietHoursEnd { get; set; } = 8; // 8 AM
    }

    // ================================
    // DASHBOARD PREFERENCES
    // ================================

    public class DashboardPreferencesDto
    {
        public string DefaultTimeRange { get; set; } = "24h";
        public List<string> VisibleWidgets { get; set; } = new();
        public Dictionary<string, int> WidgetOrder { get; set; } = new();
        public string Theme { get; set; } = "light"; // "light", "dark", "auto"
        public bool AutoRefresh { get; set; } = true;
        public int RefreshInterval { get; set; } = 30; // seconds
        public string Language { get; set; } = "pt-BR";
    }

    // ================================
    // VALIDATION HELPERS
    // ================================

    public static class DashboardValidation
    {
        public static readonly List<string> ValidTimeRanges = new() { "1h", "24h", "7d", "30d" };
        public static readonly List<string> ValidAlertTypes = new() { "error", "warning", "info" };
        public static readonly List<string> ValidSeverities = new() { "low", "medium", "high", "critical" };
        public static readonly List<string> ValidStatuses = new() { "online", "away", "offline" };
        public static readonly List<string> ValidSystemStatuses = new() { "healthy", "warning", "critical" };
        public static readonly List<string> ValidExportFormats = new() { "excel", "pdf", "csv" };

        public static bool IsValidTimeRange(string timeRange)
        {
            return ValidTimeRanges.Contains(timeRange);
        }

        public static bool IsValidAlertType(string alertType)
        {
            return ValidAlertTypes.Contains(alertType);
        }

        public static bool IsValidSeverity(string severity)
        {
            return ValidSeverities.Contains(severity);
        }
    }
}