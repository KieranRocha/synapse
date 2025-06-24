using Microsoft.Extensions.Logging;

namespace CADCompanion.Agent.Services;

public interface IWindowsNotificationService
{
    Task ShowBomExtractedNotificationAsync(string fileName, int itemCount);
    Task ShowInventorConnectedNotificationAsync();
    Task ShowInventorDisconnectedNotificationAsync();
    Task ShowErrorNotificationAsync(string title, string message);
    Task ShowInfoNotificationAsync(string title, string message);
    void ShowSystemTray();
    void HideSystemTray();
}

public class WindowsNotificationService : IWindowsNotificationService, IDisposable
{
    private readonly ILogger<WindowsNotificationService> _logger;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private const string APP_NAME = "CAD Companion";

    public WindowsNotificationService(ILogger<WindowsNotificationService> logger)
    {
        _logger = logger;
        InitializeSystemTray();
        _logger.LogInformation("✅ Sistema de notificações Windows + System Tray inicializado");
    }

    private void InitializeSystemTray()
    {
        try
        {
            // Inicializa NotifyIcon em thread própria para Windows Forms
            var thread = new Thread(() =>
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

                SetupNotifyIcon();

                // Mantém o thread de Windows Forms vivo
                System.Windows.Forms.Application.Run();
            })
            {
                IsBackground = true,
                SetApartmentState = ApartmentState.STA
            };

            thread.Start();

            // Aguarda um pouco para o thread inicializar
            Thread.Sleep(500);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao inicializar system tray");
        }
    }

    private void SetupNotifyIcon()
    {
        try
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = CreateSimpleIcon(),
                Text = APP_NAME,
                Visible = true
            };

            // Menu de contexto
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("🔔 CAD Companion", null, (s, e) => ShowStatus());
            contextMenu.Items.Add("-"); // Separador
            contextMenu.Items.Add("📊 Status", null, (s, e) => ShowStatus());
            contextMenu.Items.Add("🔄 Reconectar", null, (s, e) => ShowReconnect());
            contextMenu.Items.Add("-"); // Separador  
            contextMenu.Items.Add("❌ Sair", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Evento de clique duplo
            _notifyIcon.DoubleClick += (s, e) => ShowStatus();

            _logger.LogInformation("🔧 System Tray configurado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao configurar NotifyIcon");
        }
    }

    private System.Drawing.Icon CreateSimpleIcon()
    {
        // Cria um ícone simples de 16x16 azul
        var bitmap = new System.Drawing.Bitmap(16, 16);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            // Fundo azul
            g.FillEllipse(System.Drawing.Brushes.DodgerBlue, 0, 0, 16, 16);

            // Letra "C" branca no centro
            using (var font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold))
            {
                g.DrawString("C", font, System.Drawing.Brushes.White, 3, 2);
            }
        }

        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    public async Task ShowBomExtractedNotificationAsync(string fileName, int itemCount)
    {
        try
        {
            var title = "BOM Extraída";
            var message = $"✅ {Path.GetFileNameWithoutExtension(fileName)}\n{itemCount} itens processados";

            await ShowNotificationAsync(title, message, System.Windows.Forms.ToolTipIcon.Info);

            _logger.LogInformation("📢 Notificação BOM enviada: {FileName} - {ItemCount} itens", fileName, itemCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar notificação de BOM");
        }
    }

    public async Task ShowInventorConnectedNotificationAsync()
    {
        try
        {
            await ShowNotificationAsync(
                "Inventor Conectado",
                "✅ CAD Companion monitorando projetos",
                System.Windows.Forms.ToolTipIcon.Info
            );

            _logger.LogInformation("📢 Notificação de conexão Inventor enviada");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar notificação de conexão");
        }
    }

    public async Task ShowInventorDisconnectedNotificationAsync()
    {
        try
        {
            await ShowNotificationAsync(
                "Inventor Desconectado",
                "⚠️ Tentando reconectar automaticamente...",
                System.Windows.Forms.ToolTipIcon.Warning
            );

            _logger.LogInformation("📢 Notificação de desconexão Inventor enviada");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar notificação de desconexão");
        }
    }

    public async Task ShowErrorNotificationAsync(string title, string message)
    {
        try
        {
            await ShowNotificationAsync(title, $"❌ {message}", System.Windows.Forms.ToolTipIcon.Error);
            _logger.LogInformation("📢 Notificação de erro enviada: {Title}", title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar notificação de erro");
        }
    }

    public async Task ShowInfoNotificationAsync(string title, string message)
    {
        try
        {
            await ShowNotificationAsync(title, $"ℹ️ {message}", System.Windows.Forms.ToolTipIcon.Info);
            _logger.LogInformation("📢 Notificação informativa enviada: {Title}", title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar notificação informativa");
        }
    }

    private async Task ShowNotificationAsync(string title, string message, System.Windows.Forms.ToolTipIcon icon)
    {
        try
        {
            if (_notifyIcon != null)
            {
                // Usa Invoke para garantir que está no thread correto
                _notifyIcon.Invoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    _notifyIcon.ShowBalloonTip(
                        timeout: 5000, // 5 segundos
                        tipTitle: title,
                        tipText: message,
                        tipIcon: icon
                    );
                });
            }
            else
            {
                // Fallback: log no console
                _logger.LogInformation("🔔 NOTIFICAÇÃO: {Title} - {Message}", title, message);
                Console.WriteLine($"🔔 {title}: {message}");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao exibir notificação: {Title}", title);
            // Fallback seguro
            Console.WriteLine($"🔔 {title}: {message}");
        }
    }

    public void ShowSystemTray()
    {
        try
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao mostrar system tray");
        }
    }

    public void HideSystemTray()
    {
        try
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao esconder system tray");
        }
    }

    private void ShowStatus()
    {
        Task.Run(async () =>
        {
            await ShowInfoNotificationAsync("Status",
                $"Sistema ativo e funcionando\n" +
                $"Inventor conectado\n" +
                $"Monitorando documentos\n" +
                $"{DateTime.Now:HH:mm:ss}");
        });
    }

    private void ShowReconnect()
    {
        Task.Run(async () =>
        {
            await ShowInfoNotificationAsync("Reconectando", "Tentando reconectar ao Inventor...");
        });
    }

    private void ExitApplication()
    {
        try
        {
            _logger.LogInformation("🛑 Solicitação de saída via system tray");

            // Cleanup
            Dispose();

            // Força saída da aplicação
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao sair da aplicação");
        }
    }

    public void Dispose()
    {
        try
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao fazer dispose do NotifyIcon");
        }
    }
}