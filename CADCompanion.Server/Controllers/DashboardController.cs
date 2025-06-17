// CADCompanion.Server/Controllers/DashboardController.cs
using CADCompanion.Server.Services;
using CADCompanion.Shared.Dashboard;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;
        private readonly ISystemHealthService _healthService;

        public DashboardController(
            IDashboardService dashboardService,
            ILogger<DashboardController> logger,
            ISystemHealthService healthService)
        {
            _dashboardService = dashboardService;
            _logger = logger;
            _healthService = healthService;
        }

        /// <summary>
        /// Obtém os KPIs principais da dashboard
        /// </summary>
        [HttpGet("kpis")]
        public async Task<ActionResult<DashboardKPIsDto>> GetKPIs(
            [FromQuery] string timeRange = "24h")
        {
            try
            {
                _logger.LogInformation("📊 Requisição KPIs - TimeRange: {TimeRange}", timeRange);

                if (!DashboardValidation.IsValidTimeRange(timeRange))
                {
                    return BadRequest($"TimeRange inválido. Valores aceitos: {string.Join(", ", DashboardValidation.ValidTimeRanges)}");
                }

                var kpis = await _dashboardService.GetKPIsAsync(timeRange);

                _logger.LogInformation("✅ KPIs retornados - Projetos: {ActiveProjects}, Engenheiros: {Engineers}",
                    kpis.ActiveProjects.Value, kpis.TotalEngineers.Value);

                return Ok(kpis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar KPIs");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obtém alertas ativos do sistema
        /// </summary>
        [HttpGet("alerts")]
        public async Task<ActionResult<List<AlertDto>>> GetAlerts(
            [FromQuery] string severity = "all",
            [FromQuery] bool includeRead = false,
            [FromQuery] int limit = 50)
        {
            try
            {
                _logger.LogInformation("🚨 Requisição alertas - Severity: {Severity}, Limit: {Limit}", severity, limit);

                var alerts = await _dashboardService.GetAlertsAsync(severity, includeRead, limit);

                _logger.LogInformation("✅ {Count} alertas retornados", alerts.Count);
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar alertas");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Marca alerta como lido
        /// </summary>
        [HttpPut("alerts/{alertId}/read")]
        public async Task<ActionResult> MarkAlertAsRead(int alertId)
        {
            try
            {
                var success = await _dashboardService.MarkAlertAsReadAsync(alertId);

                if (!success)
                {
                    return NotFound($"Alerta {alertId} não encontrado");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao marcar alerta {AlertId} como lido", alertId);
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obtém atividade dos projetos ativos
        /// </summary>
        [HttpGet("project-activities")]
        public async Task<ActionResult<List<ProjectActivityDto>>> GetProjectActivities(
            [FromQuery] string timeRange = "24h",
            [FromQuery] string status = "all")
        {
            try
            {
                _logger.LogInformation("📁 Requisição atividades de projetos - TimeRange: {TimeRange}, Status: {Status}",
                    timeRange, status);

                var activities = await _dashboardService.GetProjectActivitiesAsync(timeRange, status);

                _logger.LogInformation("✅ {Count} atividades de projetos retornadas", activities.Count);
                return Ok(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar atividades de projetos");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obtém estatísticas de BOM
        /// </summary>
        [HttpGet("bom-stats")]
        public async Task<ActionResult<BOMStatsDto>> GetBOMStats(
            [FromQuery] string timeRange = "24h")
        {
            try
            {
                _logger.LogInformation("📈 Requisição estatísticas BOM - TimeRange: {TimeRange}", timeRange);

                if (!DashboardValidation.IsValidTimeRange(timeRange))
                {
                    return BadRequest($"TimeRange inválido. Valores aceitos: {string.Join(", ", DashboardValidation.ValidTimeRanges)}");
                }

                var stats = await _dashboardService.GetBOMStatsAsync(timeRange);

                _logger.LogInformation("✅ Estatísticas BOM retornadas - Extrações: {Extractions}, Taxa Sucesso: {SuccessRate}%",
                    stats.TotalExtractions, stats.SuccessRate);

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar estatísticas BOM");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obtém atividade dos engenheiros
        /// </summary>
        [HttpGet("engineers")]
        public async Task<ActionResult<List<EngineerActivityDto>>> GetEngineersActivity(
            [FromQuery] string status = "all",
            [FromQuery] string timeRange = "24h")
        {
            try
            {
                _logger.LogInformation("👥 Requisição atividade engenheiros - Status: {Status}, TimeRange: {TimeRange}",
                    status, timeRange);

                var engineers = await _dashboardService.GetEngineersActivityAsync(status, timeRange);

                _logger.LogInformation("✅ {Count} engenheiros retornados", engineers.Count);
                return Ok(engineers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar atividade dos engenheiros");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obtém status do sistema
        /// </summary>
        [HttpGet("system-status")]
        public async Task<ActionResult<SystemStatusDto>> GetSystemStatus()
        {
            try
            {
                _logger.LogDebug("🔧 Requisição status do sistema");

                var status = await _healthService.GetSystemStatusAsync();

                _logger.LogDebug("✅ Status do sistema retornado - Status: {Status}", status.Status);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar status do sistema");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obtém dados de analytics
        /// </summary>
        [HttpPost("analytics")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalytics(
            [FromBody] AnalyticsRequestDto request)
        {
            try
            {
                _logger.LogInformation("📊 Requisição analytics - Tipo: {MetricType}, Período: {StartDate} a {EndDate}",
                    request.MetricType, request.StartDate, request.EndDate);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Validação de datas
                if (request.StartDate >= request.EndDate)
                {
                    return BadRequest("Data de início deve ser menor que data de fim");
                }

                // Limite de 1 ano
                if ((request.EndDate - request.StartDate).TotalDays > 365)
                {
                    return BadRequest("Período máximo permitido é de 1 ano");
                }

                var analytics = await _dashboardService.GetAnalyticsAsync(request);

                _logger.LogInformation("✅ Analytics retornados - {DataPoints} pontos de dados",
                    analytics.DataPoints.Count);

                return Ok(analytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar analytics");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Exporta dados da dashboard
        /// </summary>
        [HttpPost("export")]
        public async Task<ActionResult<ExportResponseDto>> ExportDashboard(
            [FromBody] ExportRequestDto request)
        {
            try
            {
                _logger.LogInformation("📥 Requisição export - Tipo: {Type}, Formato: {Format}",
                    request.Type, request.Format);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (!DashboardValidation.ValidExportFormats.Contains(request.Format))
                {
                    return BadRequest($"Formato inválido. Valores aceitos: {string.Join(", ", DashboardValidation.ValidExportFormats)}");
                }

                var exportResult = await _dashboardService.ExportDashboardAsync(request);

                _logger.LogInformation("✅ Export criado - Arquivo: {FileName}, Tamanho: {Size}KB",
                    exportResult.FileName, exportResult.FileSizeBytes / 1024);

                return Ok(exportResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao exportar dashboard");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Força atualização da cache da dashboard
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult> RefreshDashboard()
        {
            try
            {
                _logger.LogInformation("🔄 Forçando refresh da dashboard");

                await _dashboardService.RefreshCacheAsync();

                _logger.LogInformation("✅ Cache da dashboard atualizada");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao refresh da dashboard");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obtém configurações de notificação do usuário
        /// </summary>
        [HttpGet("notifications/settings")]
        public async Task<ActionResult<NotificationSettingsDto>> GetNotificationSettings()
        {
            try
            {
                // TODO: Implementar autenticação para obter usuário atual
                var userId = "current-user"; // Placeholder

                var settings = await _dashboardService.GetNotificationSettingsAsync(userId);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar configurações de notificação");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Atualiza configurações de notificação do usuário
        /// </summary>
        [HttpPut("notifications/settings")]
        public async Task<ActionResult> UpdateNotificationSettings(
            [FromBody] NotificationSettingsDto settings)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // TODO: Implementar autenticação para obter usuário atual
                var userId = "current-user"; // Placeholder

                await _dashboardService.UpdateNotificationSettingsAsync(userId, settings);

                _logger.LogInformation("✅ Configurações de notificação atualizadas para usuário {UserId}", userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao atualizar configurações de notificação");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Obtém preferências da dashboard do usuário
        /// </summary>
        [HttpGet("preferences")]
        public async Task<ActionResult<DashboardPreferencesDto>> GetDashboardPreferences()
        {
            try
            {
                // TODO: Implementar autenticação para obter usuário atual
                var userId = "current-user"; // Placeholder

                var preferences = await _dashboardService.GetDashboardPreferencesAsync(userId);
                return Ok(preferences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar preferências da dashboard");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Atualiza preferências da dashboard do usuário
        /// </summary>
        [HttpPut("preferences")]
        public async Task<ActionResult> UpdateDashboardPreferences(
            [FromBody] DashboardPreferencesDto preferences)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // TODO: Implementar autenticação para obter usuário atual
                var userId = "current-user"; // Placeholder

                await _dashboardService.UpdateDashboardPreferencesAsync(userId, preferences);

                _logger.LogInformation("✅ Preferências da dashboard atualizadas para usuário {UserId}", userId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao atualizar preferências da dashboard");
                return StatusCode(500, "Erro interno do servidor");
            }
        }

        /// <summary>
        /// Health check específico para dashboard
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult> HealthCheck()
        {
            try
            {
                var isHealthy = await _dashboardService.HealthCheckAsync();

                if (isHealthy)
                {
                    return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
                }
                else
                {
                    return StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro no health check da dashboard");
                return StatusCode(503, new { status = "error", message = ex.Message, timestamp = DateTime.UtcNow });
            }
        }

        /// <summary>
        /// Obtém métricas de performance da dashboard
        /// </summary>
        [HttpGet("metrics")]
        public async Task<ActionResult> GetDashboardMetrics()
        {
            try
            {
                var metrics = await _dashboardService.GetPerformanceMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao buscar métricas da dashboard");
                return StatusCode(500, "Erro interno do servidor");
            }
        }
    }
}