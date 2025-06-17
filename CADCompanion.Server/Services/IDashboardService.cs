using CADCompanion.Shared.Dashboard;

namespace CADCompanion.Server.Services
{
    public interface IDashboardService
    {
        Task<DashboardKPIsDto> GetKPIsAsync(string timeRange);
        Task<List<AlertDto>> GetAlertsAsync(string severity, bool includeRead, int limit);
        Task<bool> MarkAlertAsReadAsync(int alertId);
        Task<List<ProjectActivityDto>> GetProjectActivitiesAsync(string timeRange, string status);
        Task<BOMStatsDto> GetBOMStatsAsync(string timeRange);
        Task<List<EngineerActivityDto>> GetEngineersActivityAsync(string status, string timeRange);
        Task<AnalyticsResponseDto> GetAnalyticsAsync(AnalyticsRequestDto request);
        Task<ExportResponseDto> ExportDashboardAsync(ExportRequestDto request);
        Task RefreshCacheAsync();
        Task<NotificationSettingsDto> GetNotificationSettingsAsync(string userId);
        Task UpdateNotificationSettingsAsync(string userId, NotificationSettingsDto settings);
        Task<DashboardPreferencesDto> GetDashboardPreferencesAsync(string userId);
        Task UpdateDashboardPreferencesAsync(string userId, DashboardPreferencesDto preferences);
        Task<bool> HealthCheckAsync();
        Task<object> GetPerformanceMetricsAsync();
    }

    public interface ISystemHealthService
    {
        Task<SystemStatusDto> GetSystemStatusAsync();
    }
}