// Em Services/CompanionWorkerService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CADCompanion.Agent.Services;

public class CompanionWorkerService : BackgroundService
{
    private readonly ILogger<CompanionWorkerService> _logger;
    private readonly IWorkDrivenMonitoringService _monitoringService;
    private readonly IApiCommunicationService _apiCommunicationService;

    public CompanionWorkerService(
        ILogger<CompanionWorkerService> logger,
        IWorkDrivenMonitoringService monitoringService,
        IApiCommunicationService apiCommunicationService)
    {
        _logger = logger;
        _monitoringService = monitoringService;
        _apiCommunicationService = apiCommunicationService;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Companion Worker Service iniciando.");
        _monitoringService.StartMonitoring();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Loop principal para tarefas de fundo, como o heartbeat
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Companion Worker executando tarefas de fundo.");
            await _apiCommunicationService.SendHeartbeatAsync();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Companion Worker Service parando.");
        _monitoringService.StopMonitoring();
        return base.StopAsync(cancellationToken);
    }
}