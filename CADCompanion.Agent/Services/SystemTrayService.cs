using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace CADCompanion.Agent.Services;

public interface ISystemTrayService
{
    void SetStatus(TrayStatus status, string tooltip = "");
    void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info);
    void UpdateTooltip(string tooltip);
}

public enum TrayStatus
{
    Connected,
    Disconnected,
    Working,
    Warning
}

public class SystemTrayService : ISystemTrayService, IDisposable
{
    private readonly ILogger<SystemTrayService> _logger;
    private readonly INotificationService _notificationService;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed = false;

    public SystemTrayService(
        ILogger<SystemTrayService> logger,
        INotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;

        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add("Status: Inicializando", null, (s, e) => OnStatusClick());
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add("Teste Notificação", null, (s, e) => OnTestNotificationClick());
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add("Sair", null, (s, e) => OnExitClick());

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "CAD Companion Agent",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.MouseClick += OnMouseClick;
            _notifyIcon.MouseDoubleClick += OnDoubleClick;

            _logger.LogInformation("System Tray inicializado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar System Tray");
        }
    }

    public void SetStatus(TrayStatus status, string tooltip = "")
    {
        if (_notifyIcon == null) return;

        try
        {
            var (icon, statusText) = status switch
            {
                TrayStatus.Connected => (SystemIcons.Information, "Conectado"),
                TrayStatus.Disconnected => (SystemIcons.Error, "Desconectado"),
                TrayStatus.Working => (SystemIcons.Application, "Trabalhando"),
                TrayStatus.Warning => (SystemIcons.Warning, "Atenção"),
                _ => (SystemIcons.Application, "Desconhecido")
            };

            _notifyIcon.Icon = icon;
            _notifyIcon.Text = string.IsNullOrEmpty(tooltip)
                ? $"CAD Companion - {statusText}"
                : $"CAD Companion - {statusText}\n{tooltip}";

            if (_contextMenu?.Items.Count > 0)
            {
                _contextMenu.Items[0].Text = $"Status: {statusText}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar status");
        }
    }

    public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
    }

    public void UpdateTooltip(string tooltip)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Text = $"CAD Companion\n{tooltip}";
        }
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _contextMenu?.Show(Cursor.Position);
        }
    }

    private void OnDoubleClick(object? sender, MouseEventArgs e)
    {
        _ = _notificationService.ShowInfoAsync("CAD Companion", "Agent executando");
    }

    private void OnStatusClick()
    {
        _ = _notificationService.ShowInfoAsync("Status", "Agent funcionando");
    }

    private void OnTestNotificationClick()
    {
        _ = _notificationService.ShowSuccessAsync("Teste", "Funcionando! 🎉");
    }

    private void OnExitClick()
    {
        _ = _notificationService.ShowInfoAsync("Finalizando", "Encerrando...");
        Task.Delay(1000).ContinueWith(_ => Application.Exit());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
            _disposed = true;
        }
    }
}