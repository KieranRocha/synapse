using CADCompanion.Server.Data;
using CADCompanion.Shared.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace CADCompanion.Server.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IProjectService _projectService;
        private readonly BomVersioningService _bomService;

        private readonly TimeSpan _defaultCacheTime = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _shortCacheTime = TimeSpan.FromMinutes(1);

        public DashboardService(
            AppDbContext context,
            ILogger<DashboardService> logger,
            IMemoryCache cache,
            IProjectService projectService,
            BomVersioningService bomService)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _projectService = projectService;
            _bomService = bomService;
        }

        public async Task<DashboardKPIsDto> GetKPIsAsync(string timeRange)
        {
            var cacheKey = $"dashboard_kpis_{timeRange}";

            if (_cache.TryGetValue(cacheKey, out DashboardKPIsDto? cachedKpis))
            {
                return cachedKpis!;
            }

            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = CalculateStartDate(endDate, timeRange);
                var previousStartDate = CalculateStartDate(startDate, timeRange);

                // Busca dados atuais e anteriores para calcular tendências
                var currentProjects = await GetActiveProjectsCount(startDate, endDate);
                var previousProjects = await GetActiveProjectsCount(previousStartDate, startDate);

                var currentEngineers = await GetActiveEngineersCount(startDate, endDate);
                var previousEngineers = await GetActiveEngineersCount(previousStartDate, startDate);

                var currentBomVersions = await GetBomVersionsCount(startDate, endDate);
                var previousBomVersions = await GetBomVersionsCount(previousStartDate, startDate);

                var systemHealth = await CalculateSystemHealth();

                var kpis = new DashboardKPIsDto
                {
                    ActiveProjects = new KPIValueDto
                    {
                        Value = currentProjects,
                        Change = currentProjects - previousProjects,
                        Trend = currentProjects >= previousProjects ? "up" : "down"
                    },
                    TotalEngineers = new KPIValueDto
                    {
                        Value = currentEngineers,
                        Change = currentEngineers - previousEngineers,
                        Trend = currentEngineers >= previousEngineers ? "up" : "down"
                    },
                    BomVersions = new KPIValueDto
                    {
                        Value = currentBomVersions,
                        Change = currentBomVersions - previousBomVersions,
                        Trend = currentBomVersions >= previousBomVersions ? "up" : "down"
                    },
                    SystemHealth = new KPIValueDto
                    {
                        Value = (int)systemHealth,
                        Change = 0, // TODO: Implementar histórico de saúde
                        Trend = "up"
                    }
                };

                _cache.Set(cacheKey, kpis, _shortCacheTime);
                return kpis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular KPIs");
                // Retorna dados mock em caso de erro
                return GetMockKPIs();
            }
        }

        public async Task<List<AlertDto>> GetAlertsAsync(string severity, bool includeRead, int limit)
        {
            try
            {
                // TODO: Implementar sistema de alertas persistente
                // Por enquanto, retorna alertas simulados baseados em dados reais
                var alerts = new List<AlertDto>();

                // Verifica projetos sem atividade
                var inactiveProjects = await GetInactiveProjects();
                foreach (var project in inactiveProjects.Take(5))
                {
                    alerts.Add(new AlertDto
                    {
                        Id = project.Id,
                        Type = "warning",
                        Message = $"Projeto {project.Name} sem atividade há {GetInactiveDays(project.LastActivity)} dias",
                        Time = GetRelativeTime(project.LastActivity ?? project.CreatedAt),
                        ProjectId = project.Id.ToString(),
                        Severity = GetInactiveDays(project.LastActivity) > 7 ? "high" : "medium",
                        CreatedAt = project.LastActivity ?? project.CreatedAt,
                        IsRead = false
                    });
                }

                // Verifica falhas de BOM
                var recentFailures = await GetRecentBOMFailures();
                foreach (var failure in recentFailures.Take(3))
                {
                    alerts.Add(new AlertDto
                    {
                        Id = failure.GetHashCode(),
                        Type = "error",
                        Message = $"Falha na extração de BOM - {failure}",
                        Time = "Hoje",
                        Severity = "high",
                        CreatedAt = DateTime.UtcNow.AddHours(-2),
                        IsRead = false
                    });
                }

                // Ordena por severidade e data
                alerts = alerts
                    .OrderByDescending(a => GetSeverityWeight(a.Severity))
                    .ThenByDescending(a => a.CreatedAt)
                    .Take(limit)
                    .ToList();

                return alerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar alertas");
                return new List<AlertDto>();
            }
        }

        public async Task<bool> MarkAlertAsReadAsync(int alertId)
        {
            try
            {
                // TODO: Implementar persistência de alertas
                _logger.LogInformation("Alerta {AlertId} marcado como lido", alertId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao marcar alerta como lido");
                return false;
            }
        }

        public async Task<List<ProjectActivityDto>> GetProjectActivitiesAsync(string timeRange, string status)
        {
            try
            {
                var projects = await _projectService.GetActiveProjectsAsync();
                var activities = new List<ProjectActivityDto>();

                foreach (var project in projects)
                {
                    var bomCount = await GetProjectBomCount(project.Id);
                    var lastBom = await GetLastBomExtraction(project.Id);

                    activities.Add(new ProjectActivityDto
                    {
                        Id = project.Id,
                        Name = project.Name,
                        Activity = project.ProgressPercentage,
                        Status = project.Status.ToLowerInvariant(),
                        Deadline = CalculateDeadlineText(project.EndDate),
                        Budget = CalculateBudgetUsage(project.BudgetValue, project.ActualCost),
                        LastActivity = GetRelativeTime(project.LastActivity ?? project.CreatedAt),
                        ResponsibleEngineer = project.ResponsibleEngineer,
                        TotalBomVersions = bomCount,
                        LastBomExtraction = lastBom
                    });
                }

                return activities
                    .OrderByDescending(a => a.LastBomExtraction ?? DateTime.MinValue)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar atividades de projetos");
                return new List<ProjectActivityDto>();
            }
        }

        public async Task<BOMStatsDto> GetBOMStatsAsync(string timeRange)
        {
            var cacheKey = $"bom_stats_{timeRange}";

            if (_cache.TryGetValue(cacheKey, out BOMStatsDto? cachedStats))
            {
                return cachedStats!;
            }

            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = CalculateStartDate(endDate, timeRange);

                var totalExtractions = await _context.BomVersions
                    .Where(b => b.ExtractedAt >= startDate && b.ExtractedAt <= endDate)
                    .CountAsync();

                var failedExtractions = 0; // TODO: Implementar tracking de falhas

                var avgProcessingTime = await CalculateAverageProcessingTime(startDate, endDate);
                var lastHourExtractions = await GetLastHourExtractions();
                var systemAvailability = await CalculateSystemAvailability();

                var stats = new BOMStatsDto
                {
                    TotalExtractions = totalExtractions,
                    SuccessRate = totalExtractions > 0 ? ((double)(totalExtractions - failedExtractions) / totalExtractions * 100) : 100,
                    AvgProcessingTime = avgProcessingTime,
                    LastHour = lastHourExtractions,
                    FailedExtractions = failedExtractions,
                    SystemAvailability = systemAvailability,
                    HourlyTrend = await GetHourlyTrend(startDate, endDate)
                };

                _cache.Set(cacheKey, stats, _shortCacheTime);
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular estatísticas BOM");
                return GetMockBOMStats();
            }
        }

        public async Task<List<EngineerActivityDto>> GetEngineersActivityAsync(string status, string timeRange)
        {
            try
            {
                // TODO: Implementar tracking real de engenheiros
                // Por enquanto, simula baseado em dados de BOMs e projetos
                var engineers = new List<EngineerActivityDto>();

                var recentBoms = await _context.BomVersions
                    .Where(b => b.ExtractedAt >= DateTime.UtcNow.AddHours(-24))
                    .GroupBy(b => b.ExtractedBy)
                    .Select(g => new { Engineer = g.Key, Count = g.Count(), LastActivity = g.Max(b => b.ExtractedAt) })
                    .ToListAsync();

                foreach (var bomGroup in recentBoms)
                {
                    var projectsWorked = await _context.BomVersions
                        .Where(b => b.ExtractedBy == bomGroup.Engineer &&
                                   b.ExtractedAt >= DateTime.UtcNow.AddHours(-24))
                        .Select(b => b.ProjectId)
                        .Distinct()
                        .CountAsync();

                    engineers.Add(new EngineerActivityDto
                    {
                        Id = bomGroup.Engineer.GetHashCode(),
                        Name = bomGroup.Engineer,
                        Projects = projectsWorked,
                        Saves = bomGroup.Count * 5, // Estimativa: 5 saves por BOM
                        Hours = CalculateWorkHours(bomGroup.Count),
                        Status = GetEngineerStatus(bomGroup.LastActivity),
                        LastActivity = GetRelativeTime(bomGroup.LastActivity),
                        WorkstationId = bomGroup.Engineer,
                        ActiveProjects = new List<EngineerProjectDto>()
                    });
                }

                return engineers.OrderByDescending(e => e.Hours).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar atividade dos engenheiros");
                return new List<EngineerActivityDto>();
            }
        }

        public async Task<AnalyticsResponseDto> GetAnalyticsAsync(AnalyticsRequestDto request)
        {
            try
            {
                var dataPoints = new List<AnalyticsDataPointDto>();

                switch (request.MetricType.ToLower())
                {
                    case "productivity":
                        dataPoints = await CalculateProductivityMetrics(request);
                        break;
                    case "quality":
                        dataPoints = await CalculateQualityMetrics(request);
                        break;
                    case "efficiency":
                        dataPoints = await CalculateEfficiencyMetrics(request);
                        break;
                    default:
                        throw new ArgumentException($"Tipo de métrica não suportado: {request.MetricType}");
                }

                return new AnalyticsResponseDto
                {
                    MetricType = request.MetricType,
                    DataPoints = dataPoints,
                    Summary = CalculateSummary(dataPoints),
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular analytics");
                throw;
            }
        }

        public async Task<ExportResponseDto> ExportDashboardAsync(ExportRequestDto request)
        {
            try
            {
                var fileName = $"dashboard_export_{DateTime.Now:yyyyMMdd_HHmmss}.{request.Format}";
                var filePath = Path.Combine("exports", fileName);

                // TODO: Implementar exportação real
                Directory.CreateDirectory("exports");
                await File.WriteAllTextAsync(filePath, "Export placeholder");

                return new ExportResponseDto
                {
                    FileName = fileName,
                    DownloadUrl = $"/api/dashboard/download/{fileName}",
                    FileSizeBytes = 1024,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    Format = request.Format
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao exportar dashboard");
                throw;
            }
        }

        public async Task RefreshCacheAsync()
        {
            try
            {
                var cacheKeys = new[] { "dashboard_kpis_24h", "bom_stats_24h", "dashboard_kpis_1h", "bom_stats_1h" };

                foreach (var key in cacheKeys)
                {
                    _cache.Remove(key);
                }

                _logger.LogInformation("Cache da dashboard limpa");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar cache");
                throw;
            }
        }

        public async Task<NotificationSettingsDto> GetNotificationSettingsAsync(string userId)
        {
            // TODO: Implementar persistência de configurações
            return new NotificationSettingsDto
            {
                EnableRealTime = true,
                EnableEmail = false,
                AlertTypes = new List<string> { "error", "warning" },
                ProjectIds = new List<string>(),
                EmailAddress = "",
                QuietHoursStart = 18,
                QuietHoursEnd = 8
            };
        }

        public async Task UpdateNotificationSettingsAsync(string userId, NotificationSettingsDto settings)
        {
            // TODO: Implementar persistência
            await Task.CompletedTask;
        }

        public async Task<DashboardPreferencesDto> GetDashboardPreferencesAsync(string userId)
        {
            // TODO: Implementar persistência
            return new DashboardPreferencesDto
            {
                DefaultTimeRange = "24h",
                VisibleWidgets = new List<string> { "kpis", "alerts", "projects", "bom", "engineers" },
                WidgetOrder = new Dictionary<string, int>(),
                Theme = "light",
                AutoRefresh = true,
                RefreshInterval = 30,
                Language = "pt-BR"
            };
        }

        public async Task UpdateDashboardPreferencesAsync(string userId, DashboardPreferencesDto preferences)
        {
            // TODO: Implementar persistência
            await Task.CompletedTask;
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<object> GetPerformanceMetricsAsync()
        {
            var process = Process.GetCurrentProcess();

            return new
            {
                CpuUsage = 0, // TODO: Calcular uso real de CPU
                MemoryUsage = process.WorkingSet64 / (1024 * 1024), // MB
                ActiveConnections = 0, // TODO: Contar conexões ativas
                CacheSize = _cache.GetType().GetFields().Length,
                DatabaseConnections = await _context.Database.CanConnectAsync(),
                Timestamp = DateTime.UtcNow
            };
        }

        #region Helper Methods

        private DateTime CalculateStartDate(DateTime endDate, string timeRange)
        {
            return timeRange switch
            {
                "1h" => endDate.AddHours(-1),
                "24h" => endDate.AddHours(-24),
                "7d" => endDate.AddDays(-7),
                "30d" => endDate.AddDays(-30),
                _ => endDate.AddHours(-24)
            };
        }

        private async Task<int> GetActiveProjectsCount(DateTime startDate, DateTime endDate)
        {
            return await _context.Projects
                .Where(p => p.Status == Models.ProjectStatus.Active &&
                           (p.LastActivity >= startDate || p.CreatedAt >= startDate))
                .CountAsync();
        }

        private async Task<int> GetActiveEngineersCount(DateTime startDate, DateTime endDate)
        {
            return await _context.BomVersions
                .Where(b => b.ExtractedAt >= startDate && b.ExtractedAt <= endDate)
                .Select(b => b.ExtractedBy)
                .Distinct()
                .CountAsync();
        }

        private async Task<int> GetBomVersionsCount(DateTime startDate, DateTime endDate)
        {
            return await _context.BomVersions
                .Where(b => b.ExtractedAt >= startDate && b.ExtractedAt <= endDate)
                .CountAsync();
        }

        private async Task<double> CalculateSystemHealth()
        {
            // TODO: Implementar cálculo real baseado em métricas do sistema
            return 98.2;
        }

        private DashboardKPIsDto GetMockKPIs()
        {
            return new DashboardKPIsDto
            {
                ActiveProjects = new KPIValueDto { Value = 23, Change = 2, Trend = "up" },
                TotalEngineers = new KPIValueDto { Value = 47, Change = 3, Trend = "up" },
                BomVersions = new KPIValueDto { Value = 1247, Change = 89, Trend = "up" },
                SystemHealth = new KPIValueDto { Value = 98, Change = 0, Trend = "up" }
            };
        }

        private BOMStatsDto GetMockBOMStats()
        {
            return new BOMStatsDto
            {
                TotalExtractions = 2847,
                SuccessRate = 97.8,
                AvgProcessingTime = 3.2,
                LastHour = 47,
                FailedExtractions = 23,
                SystemAvailability = 98.2,
                HourlyTrend = new List<BOMExtractionTrendDto>()
            };
        }

        private async Task<List<Models.Project>> GetInactiveProjects()
        {
            return await _context.Projects
                .Where(p => p.Status == Models.ProjectStatus.Active &&
                           (p.LastActivity == null || p.LastActivity < DateTime.UtcNow.AddDays(-3)))
                .ToListAsync();
        }

        private int GetInactiveDays(DateTime? lastActivity)
        {
            if (!lastActivity.HasValue) return 999;
            return (int)(DateTime.UtcNow - lastActivity.Value).TotalDays;
        }

        private string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalMinutes < 1) return "Agora";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} min atrás";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h atrás";
            return $"{(int)timeSpan.TotalDays} dias atrás";
        }

        private async Task<List<string>> GetRecentBOMFailures()
        {
            // TODO: Implementar tracking de falhas
            return new List<string> { "Máquina M-340", "Assembly_Complex.iam" };
        }

        private int GetSeverityWeight(string severity)
        {
            return severity switch
            {
                "critical" => 4,
                "high" => 3,
                "medium" => 2,
                "low" => 1,
                _ => 0
            };
        }

        private string CalculateDeadlineText(DateTime? endDate)
        {
            if (!endDate.HasValue) return "Indefinido";

            var daysLeft = (int)(endDate.Value - DateTime.UtcNow).TotalDays;

            if (daysLeft < 0) return "Atrasado";
            if (daysLeft == 0) return "Hoje";
            if (daysLeft == 1) return "Amanhã";
            return $"{daysLeft} dias";
        }

        private int CalculateBudgetUsage(decimal? budget, decimal? actualCost)
        {
            if (!budget.HasValue || budget.Value == 0) return 0;
            if (!actualCost.HasValue) return 0;

            return (int)((actualCost.Value / budget.Value) * 100);
        }

        private async Task<int> GetProjectBomCount(int projectId)
        {
            return await _context.BomVersions
                .Where(b => b.ProjectId == projectId.ToString())
                .CountAsync();
        }

        private async Task<DateTime?> GetLastBomExtraction(int projectId)
        {
            return await _context.BomVersions
                .Where(b => b.ProjectId == projectId.ToString())
                .OrderByDescending(b => b.ExtractedAt)
                .Select(b => b.ExtractedAt)
                .FirstOrDefaultAsync();
        }

        private async Task<double> CalculateAverageProcessingTime(DateTime startDate, DateTime endDate)
        {
            // TODO: Implementar tracking de tempo de processamento
            return 3.2;
        }

        private async Task<int> GetLastHourExtractions()
        {
            return await _context.BomVersions
                .Where(b => b.ExtractedAt >= DateTime.UtcNow.AddHours(-1))
                .CountAsync();
        }

        private async Task<double> CalculateSystemAvailability()
        {
            // TODO: Implementar cálculo real
            return 98.2;
        }

        private async Task<List<BOMExtractionTrendDto>> GetHourlyTrend(DateTime startDate, DateTime endDate)
        {
            // TODO: Implementar trend real
            return new List<BOMExtractionTrendDto>();
        }

        private string GetEngineerStatus(DateTime lastActivity)
        {
            var timeSince = DateTime.UtcNow - lastActivity;

            if (timeSince.TotalMinutes < 15) return "online";
            if (timeSince.TotalMinutes < 60) return "away";
            return "offline";
        }

        private double CalculateWorkHours(int bomCount)
        {
            // Estimativa: 30 minutos por BOM
            return bomCount * 0.5;
        }

        private async Task<List<AnalyticsDataPointDto>> CalculateProductivityMetrics(AnalyticsRequestDto request)
        {
            // TODO: Implementar métricas de produtividade
            return new List<AnalyticsDataPointDto>();
        }

        private async Task<List<AnalyticsDataPointDto>> CalculateQualityMetrics(AnalyticsRequestDto request)
        {
            // TODO: Implementar métricas de qualidade
            return new List<AnalyticsDataPointDto>();
        }

        private async Task<List<AnalyticsDataPointDto>> CalculateEfficiencyMetrics(AnalyticsRequestDto request)
        {
            // TODO: Implementar métricas de eficiência
            return new List<AnalyticsDataPointDto>();
        }

        private Dictionary<string, object> CalculateSummary(List<AnalyticsDataPointDto> dataPoints)
        {
            return new Dictionary<string, object>
            {
                ["count"] = dataPoints.Count,
                ["average"] = dataPoints.Count > 0 ? dataPoints.Average(d => d.Value) : 0,
                ["min"] = dataPoints.Count > 0 ? dataPoints.Min(d => d.Value) : 0,
                ["max"] = dataPoints.Count > 0 ? dataPoints.Max(d => d.Value) : 0
            };
        }

        #endregion
    }
}