// CADCompanion.Agent/Services/CompanionWorkerService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CADCompanion.Agent.Services
{
    public class CompanionWorkerService : BackgroundService
    {
        private readonly ILogger<CompanionWorkerService> _logger;
        private readonly IInventorConnectionService _connectionService;
        private readonly IInventorDocumentEventService _eventService;
        private readonly IDocumentProcessingService _processingService;
        private readonly IWorkDrivenMonitoringService _monitoringService;

        public CompanionWorkerService(
            ILogger<CompanionWorkerService> logger,
            IInventorConnectionService connectionService,
            IInventorDocumentEventService eventService,
            IDocumentProcessingService processingService, // Serviço de processamento injetado
            IWorkDrivenMonitoringService monitoringService)
        {
            _logger = logger;
            _connectionService = connectionService;
            _eventService = eventService;
            _processingService = processingService;
            _monitoringService = monitoringService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Companion Worker Service iniciando.");

            try
            {
                // 1. Conecta ao Inventor
                _connectionService.Connect();

                // 2. ✅ LIGAÇÃO: Inscreve o DocumentProcessingService para ouvir os eventos do Inventor.
                //    Quando um documento for aberto ou salvo, o método ProcessDocumentEventAsync será chamado.
                _eventService.DocumentOpened += async (s, e) => await _processingService.ProcessDocumentEventAsync(e, "DocumentOpened");
                _eventService.DocumentSaved += async (s, e) => await _processingService.ProcessDocumentEventAsync(e, "DocumentSaved");
                _eventService.DocumentClosed += async (s, e) => await _processingService.ProcessDocumentEventAsync(e, "DocumentClosed");

                // 3. Inicia o monitoramento de eventos do Inventor (que agora acionarão o processador)
                _eventService.Start();

                // 4. Inicia o monitoramento de arquivos em pastas
                _monitoringService.StartMonitoring();

                _logger.LogInformation("✅ Todos os serviços foram iniciados e conectados. Agente está ativo.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Falha crítica ao inicializar os serviços do Inventor. O agente será encerrado.");
                return; // Encerra o serviço se a inicialização falhar
            }

            // Mantém o serviço rodando em segundo plano
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Companion Worker Service parando.");
            _monitoringService.StopMonitoring();
            _eventService.Stop();
            // Para um serviço singleton que dura a vida toda da app,
            // a desinscrição de eventos não é crítica, mas é uma boa prática.
            // Para simplicidade, foi omitida aqui.
            await base.StopAsync(cancellationToken);
        }
    }
}