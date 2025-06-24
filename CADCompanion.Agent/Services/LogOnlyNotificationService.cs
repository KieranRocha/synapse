// CADCompanion.Agent/Services/LogOnlyNotificationService.cs
// Serviço de notificações para quando roda como Windows Service
using Microsoft.Extensions.Logging;

namespace CADCompanion.Agent.Services
{
    /// <summary>
    /// Implementação de notificações que apenas registra nos logs
    /// Usado quando o aplicativo roda como Windows Service
    /// </summary>
    public class LogOnlyNotificationService : IWindowsNotificationService
    {
        private readonly ILogger<LogOnlyNotificationService> _logger;

        public LogOnlyNotificationService(ILogger<LogOnlyNotificationService> logger)
        {
            _logger = logger;
            _logger.LogInformation("🔧 Modo Serviço: Notificações serão registradas apenas nos logs");
        }

        public void ShowDocumentOpenedNotification(string fileName, string projectName, int machineId)
        {
            _logger.LogInformation("📁 [NOTIFICAÇÃO] Documento Aberto: {FileName} | Projeto: {ProjectName} | Máquina ID: {MachineId}",
                fileName, projectName, machineId);
        }

        public void ShowBOMExtractionNotification(string fileName, int itemCount)
        {
            _logger.LogInformation("✅ [NOTIFICAÇÃO] BOM Extraído: {FileName} | Itens: {ItemCount}",
                fileName, itemCount);
        }

        public void ShowSimpleNotification(string title, string message)
        {
            _logger.LogInformation("ℹ️ [NOTIFICAÇÃO] {Title}: {Message}", title, message);
        }

        public void ShowWarningNotification(string title, string message)
        {
            _logger.LogWarning("⚠️ [NOTIFICAÇÃO] {Title}: {Message}", title, message);
        }

        public void ShowSuccessNotification(string title, string message)
        {
            _logger.LogInformation("✅ [NOTIFICAÇÃO] {Title}: {Message}", title, message);
        }
    }
}