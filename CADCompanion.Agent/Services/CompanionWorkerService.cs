// Em Services/CompanionWorkerService.cs - CORRIGIDO
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CADCompanion.Agent.Services;

public class CompanionWorkerService : BackgroundService
{
    private readonly ILogger<CompanionWorkerService> _logger;
    private readonly IWorkDrivenMonitoringService _monitoringService;
    private readonly IApiCommunicationService _apiCommunicationService;
    private readonly IInventorConnectionService _inventorConnectionService; // ✅ 1. Adicionar

    public CompanionWorkerService(
        ILogger<CompanionWorkerService> logger,
        IWorkDrivenMonitoringService monitoringService,
        IApiCommunicationService apiCommunicationService,
        IInventorConnectionService inventorConnectionService) // ✅ 2. Adicionar ao construtor
    {
        _logger = logger;
        _monitoringService = monitoringService;
        _apiCommunicationService = apiCommunicationService;
        _inventorConnectionService = inventorConnectionService; // ✅ 3. Atribuir
    }

    public override async Task StartAsync(CancellationToken cancellationToken) // ✅ 4. Marcar como async
    {
        _logger.LogInformation("Companion Worker Service iniciando.");

        try
        {
            // ✅ 5. Conectar ao Inventor ANTES de qualquer outra coisa
            await _inventorConnectionService.ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Falha crítica ao conectar com o Inventor. A aplicação será encerrada.");
            // Opcional: Você pode querer parar a aplicação aqui se o Inventor for essencial.
            // _applicationLifetime.StopApplication(); 
            return;
        }

        // Só inicia o monitoramento se a conexão for bem sucedida
        if (_inventorConnectionService.IsConnected)
        {
            _monitoringService.StartMonitoring();
        }
        else
        {
            _logger.LogWarning("Monitoramento não iniciado pois a conexão com o Inventor falhou.");
        }

        await base.StartAsync(cancellationToken); // ✅ 6. Usar await
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Loop principal para tarefas de fundo, como o heartbeat
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Companion Worker executando tarefas de fundo.");

            // Só envia heartbeat se o servidor estiver acessível
            if (_inventorConnectionService.IsConnected)
            {
                await _apiCommunicationService.SendHeartbeatAsync();
            }

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