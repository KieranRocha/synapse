// CADCompanion.Agent/Services/SimpleNotificationService.cs
// VERSÃO ALTERNATIVA se Windows Forms não funcionar
using Microsoft.Extensions.Logging;
using System;
using System.Windows.Forms;

namespace CADCompanion.Agent.Services
{

    /// <summary>
    /// Versão simplificada usando apenas MessageBox
    /// Use esta se NotifyIcon não funcionar
    /// </summary>
    public class SimpleNotificationService : IWindowsNotificationService
    {
        private readonly ILogger<SimpleNotificationService> _logger;

        public SimpleNotificationService(ILogger<SimpleNotificationService> logger)
        {
            _logger = logger;
            _logger.LogInformation("🔔 Sistema de notificações simplificado inicializado");
        }

        public void ShowDocumentOpenedNotification(string fileName, string projectName, int machineId)
        {
            try
            {
                var message = $"📁 Documento Aberto\n\nArquivo: {fileName}\nProjeto: {projectName}\nMáquina ID: {machineId}";

                ShowMessageBox("CAD Companion", message, MessageBoxIcon.Information);
                _logger.LogInformation("✅ Notificação de documento aberto enviada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao mostrar notificação de documento aberto");
            }
        }

        public void ShowBOMExtractionNotification(string fileName, int itemCount)
        {
            try
            {
                var message = $"✅ BOM Extraído\n\nArquivo: {fileName}\nItens: {itemCount}\nDados enviados para o servidor";

                ShowMessageBox("CAD Companion", message, MessageBoxIcon.Information);
                _logger.LogInformation("✅ Notificação de BOM extraído enviada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao mostrar notificação de BOM extraído");
            }
        }

        public void ShowSimpleNotification(string title, string message)
        {
            try
            {
                ShowMessageBox(title, message, MessageBoxIcon.Information);
                _logger.LogDebug($"ℹ️ Notificação simples: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mostrar notificação simples: {title}");
            }
        }

        public void ShowWarningNotification(string title, string message)
        {
            try
            {
                ShowMessageBox(title, message, MessageBoxIcon.Warning);
                _logger.LogWarning($"⚠️ Notificação de aviso: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mostrar notificação de aviso: {title}");
            }
        }

        public void ShowSuccessNotification(string title, string message)
        {
            try
            {
                ShowMessageBox(title, message, MessageBoxIcon.Information);
                _logger.LogInformation($"✅ Notificação de sucesso: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mostrar notificação de sucesso: {title}");
            }
        }

        private void ShowMessageBox(string title, string message, MessageBoxIcon icon)
        {
            try
            {
                // Executa em thread separada para não bloquear
                Task.Run(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mostrar MessageBox: {title}");

                // Fallback para console
                Console.WriteLine($"NOTIFICAÇÃO: {title} - {message}");
            }
        }
    }
}