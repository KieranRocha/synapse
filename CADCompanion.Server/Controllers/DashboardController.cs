// CADCompanion.Server/Controllers/DashboardController.cs - IMPORTS CORRIGIDOS
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
    }
}