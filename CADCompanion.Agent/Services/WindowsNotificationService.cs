// CADCompanion.Agent/Services/WindowsNotificationService.cs
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace CADCompanion.Agent.Services
{
    public interface IWindowsNotificationService
    {
        void ShowDocumentOpenedNotification(string fileName, string projectName, int machineId);
        void ShowBOMExtractionNotification(string fileName, int itemCount);
        void ShowSimpleNotification(string title, string message);
        void ShowWarningNotification(string title, string message);
        void ShowSuccessNotification(string title, string message);
    }

    public class WindowsNotificationService : IWindowsNotificationService, IDisposable
    {
        private readonly ILogger<WindowsNotificationService> _logger;
        private readonly NotifyIcon _notifyIcon;

        public WindowsNotificationService(ILogger<WindowsNotificationService> logger)
        {
            _logger = logger;

            // Configura o ícone da bandeja do sistema
            _notifyIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "CAD Companion Agent",
                BalloonTipTitle = "CAD Companion"
            };

            // Adiciona menu de contexto
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("CAD Companion Agent", null, (s, e) => { });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Status: Ativo", null, (s, e) => { });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Sair", null, (s, e) => Application.Exit());

            _notifyIcon.ContextMenuStrip = contextMenu;

            _logger.LogInformation("🔔 Sistema de notificações Windows inicializado");
        }

        public void ShowDocumentOpenedNotification(string fileName, string projectName, int machineId)
        {
            try
            {
                var title = "📁 Documento Aberto";
                var message = $"Arquivo: {fileName}\nProjeto: {projectName}\nMáquina ID: {machineId}";

                ShowBalloonTip(title, message, ToolTipIcon.Info, 5000);
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
                var title = "✅ BOM Extraído";
                var message = $"Arquivo: {fileName}\nItens: {itemCount}\nDados enviados para o servidor";

                ShowBalloonTip(title, message, ToolTipIcon.Info, 4000);
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
                ShowBalloonTip(title, message, ToolTipIcon.Info, 3000);
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
                ShowBalloonTip(title, message, ToolTipIcon.Warning, 6000);
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
                ShowBalloonTip(title, message, ToolTipIcon.Info, 3000);
                _logger.LogInformation($"✅ Notificação de sucesso: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao mostrar notificação de sucesso: {title}");
            }
        }

        private void ShowBalloonTip(string title, string message, ToolTipIcon icon, int timeout)
        {
            try
            {
                _notifyIcon.BalloonTipIcon = icon;
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = message;
                _notifyIcon.ShowBalloonTip(timeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao mostrar balloon tip");
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void Dispose()
        {
            try
            {
                _notifyIcon?.Dispose();
                _logger.LogInformation("🔔 Sistema de notificações Windows finalizado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao finalizar sistema de notificações");
            }
        }
    }
}