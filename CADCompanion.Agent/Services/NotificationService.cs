using Microsoft.Extensions.Logging;
using System.Windows.Forms;

namespace CADCompanion.Agent.Services;

public interface INotificationService
{
    Task ShowSuccessAsync(string title, string message);
    Task ShowWarningAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task ShowInfoAsync(string title, string message);
}

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly NotifyIcon _notifyIcon;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;

        // Inicializar NotifyIcon uma vez
        _notifyIcon = new NotifyIcon();
        _notifyIcon.Visible = true;
        _notifyIcon.Icon = SystemIcons.Application;
        _notifyIcon.Text = "CAD Companion Agent";
    }

    public async Task ShowSuccessAsync(string title, string message)
    {
        await ShowBalloonAsync(title, message, ToolTipIcon.Info, "✅");
    }

    public async Task ShowWarningAsync(string title, string message)
    {
        await ShowBalloonAsync(title, message, ToolTipIcon.Warning, "⚠️");
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        await ShowBalloonAsync(title, message, ToolTipIcon.Error, "❌");
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        await ShowBalloonAsync(title, message, ToolTipIcon.Info, "ℹ️");
    }

    private async Task ShowBalloonAsync(string title, string message, ToolTipIcon icon, string emoji)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                $"{emoji} {title}",
                message,
                icon
            );

            _logger.LogDebug($"Balloon tip enviado: {title} - {message}");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao enviar notificação: {title}");
        }
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}