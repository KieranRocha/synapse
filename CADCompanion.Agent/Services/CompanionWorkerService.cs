// Services/CompanionWorkerService.cs - COM SYSTEM TRAY E NOTIFICAÇÕES FUNCIONAIS
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CADCompanion.Agent.Services;

public class CompanionWorkerService : BackgroundService
{
    private readonly ILogger<CompanionWorkerService> _logger;
    private readonly IWorkDrivenMonitoringService _monitoringService;
    private readonly IApiCommunicationService _apiCommunicationService;
    private readonly IInventorConnectionService _inventorConnectionService;
    private readonly IWindowsNotificationService _notificationService;

    public CompanionWorkerService(
        ILogger<CompanionWorkerService> logger,
        IWorkDrivenMonitoringService monitoringService,
        IApiCommunicationService apiCommunicationService,
        IInventorConnectionService inventorConnectionService,
        IWindowsNotificationService notificationService)
    {
        _logger = logger;
        _monitoringService = monitoringService;
        _apiCommunicationService = apiCommunicationService;
        _inventorConnectionService = inventorConnectionService;
        _notificationService = notificationService;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 Companion Worker Service iniciando.");

        try
        {
            // ✅ MOSTRA SYSTEM TRAY IMEDIATAMENTE
            _notificationService.ShowSystemTray();

            // ✅ NOTIFICAÇÃO DE INICIALIZAÇÃO FUNCIONAL
            await _notificationService.ShowInfoNotificationAsync(
                "CAD Companion Iniciado",
                "Sistema carregado com sucesso!\nConectando ao Inventor..."
            );

            // Pequena pausa para garantir que a notificação apareça
            await Task.Delay(1000, cancellationToken);

            // Conectar ao Inventor
            await _inventorConnectionService.ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "❌ Falha crítica ao conectar com o Inventor");

            // ✅ NOTIFICAÇÃO DE ERRO FUNCIONAL
            await _notificationService.ShowErrorNotificationAsync(
                "Erro Crítico",
                "Falha ao conectar com o Inventor.\nVerifique se o Inventor está aberto."
            );

            return;
        }

        // Só inicia o monitoramento se a conexão for bem sucedida
        if (_inventorConnectionService.IsConnected)
        {
            _monitoringService.StartMonitoring();

            // ✅ NOTIFICAÇÃO DE SUCESSO FUNCIONAL
            await _notificationService.ShowInventorConnectedNotificationAsync();
        }
        else
        {
            _logger.LogWarning("⚠️ Monitoramento não iniciado - conexão com Inventor falhou");

            // ✅ NOTIFICAÇÃO DE AVISO FUNCIONAL
            await _notificationService.ShowErrorNotificationAsync(
                "Aviso do Sistema",
                "Monitoramento não pôde ser iniciado.\nVerifique a conexão com o Inventor."
            );
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("💓 Loop principal do Companion Worker iniciado");

        var heartbeatCounter = 0;
        var notificationCounter = 0;

        // Loop principal para tarefas de fundo
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("🔄 Companion Worker executando tarefas de fundo");

                // Só envia heartbeat se estiver conectado
                if (_inventorConnectionService.IsConnected)
                {
                    await _apiCommunicationService.SendHeartbeatAsync();
                    heartbeatCounter++;

                    // ✅ NOTIFICAÇÃO PERIÓDICA DE STATUS (A CADA 1 HORA = 12 CICLOS DE 5 MIN)
                    if (heartbeatCounter % 12 == 0)
                    {
                        notificationCounter++;
                        await _notificationService.ShowInfoNotificationAsync(
                            "Sistema Ativo",
                            $"✅ Monitoramento funcionando\n" +
                            $"🔗 Inventor conectado\n" +
                            $"📊 Ciclo #{notificationCounter} completado"
                        );
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ Inventor desconectado - tentando reconectar");

                    try
                    {
                        await _inventorConnectionService.ConnectAsync();
                        if (_inventorConnectionService.IsConnected)
                        {
                            // ✅ NOTIFICAÇÃO DE RECONEXÃO BEM-SUCEDIDA
                            await _notificationService.ShowInfoNotificationAsync(
                                "Reconectado com Sucesso",
                                "✅ Conexão com Inventor restaurada\n📁 Monitoramento reativado"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Falha na tentativa de reconexão");

                        // ✅ NOTIFICAÇÃO DE ERRO DE RECONEXÃO (1x por hora para não spam)
                        if (heartbeatCounter % 12 == 0)
                        {
                            await _notificationService.ShowErrorNotificationAsync(
                                "Erro de Conexão",
                                "❌ Não foi possível reconectar ao Inventor\n🔄 Tentativas continuam automaticamente"
                            );
                        }
                    }
                }

                // ✅ AGUARDA 5 MINUTOS - INTERVALO AJUSTADO PARA TESTES
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Cancelamento normal - não loga como erro
                _logger.LogInformation("🛑 Operação cancelada - parando loop principal");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro inesperado no loop principal");

                // ✅ NOTIFICAÇÃO DE ERRO NO LOOP PRINCIPAL
                await _notificationService.ShowErrorNotificationAsync(
                    "Erro no Sistema",
                    "❌ Erro inesperado no monitoramento\n🔄 Sistema continuará tentando"
                );

                // Aguarda antes de tentar novamente
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 Companion Worker Service parando");

        try
        {
            // ✅ NOTIFICAÇÃO DE PARADA
            await _notificationService.ShowInfoNotificationAsync(
                "Sistema Finalizando",
                "🛑 CAD Companion sendo encerrado\n💾 Salvando dados..."
            );

            // Para o monitoramento
            _monitoringService.StopMonitoring();

            // Aguarda um pouco para a notificação aparecer
            await Task.Delay(2000, cancellationToken);

            // Remove da system tray
            _notificationService.HideSystemTray();

            _logger.LogInformation("✅ Monitoramento parado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao parar o monitoramento");
        }

        await base.StopAsync(cancellationToken);
    }
}