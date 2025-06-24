// Program.cs - CORRIGIDO v4 - COM NOTIFICAÇÕES
using CADCompanion.Agent.Configuration;
using CADCompanion.Agent.Services;
using CADCompanion.Agent; // Para InventorBomExtractor
using Serilog;
using System.Windows.Forms; // ✅ ADICIONAR

// ✅ ADICIONAR - Necessário para Windows Forms
[STAThread]
static async Task Main(string[] args)
{
    // ✅ ADICIONAR - Habilita Windows Forms (apenas se não for serviço)
    bool runAsService = args.Contains("--service") || !Environment.UserInteractive;

    if (!runAsService)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }

    var hostBuilder = Host.CreateDefaultBuilder(args);

    // Só usa Windows Service se especificado
    if (runAsService)
    {
        hostBuilder.UseWindowsService(options =>
        {
            options.ServiceName = "CAD Companion Agent";
        });
    }

    IHost host = hostBuilder
        .UseSerilog((context, loggerConfig) =>
        {
            loggerConfig
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/companion-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30);
        })
        .ConfigureServices((hostContext, services) =>
        {
            // Configuração
            services.Configure<CompanionConfiguration>(hostContext.Configuration.GetSection("CompanionConfiguration"));

            // HttpClient para API
            services.AddHttpClient<IApiCommunicationService, ApiCommunicationService>(client =>
            {
                var serverUrl = hostContext.Configuration["ServerBaseUrl"];
                if (string.IsNullOrEmpty(serverUrl))
                {
                    throw new InvalidOperationException("A URL do servidor (ServerBaseUrl) não foi definida.");
                }
                client.BaseAddress = new Uri(serverUrl);
            });

            // Registra os serviços como Singleton
            services.AddSingleton<InventorBomExtractor>();
            services.AddSingleton<IInventorBOMExtractor>(provider => provider.GetRequiredService<InventorBomExtractor>());

            services.AddSingleton<IInventorConnectionService, InventorConnectionService>();
            services.AddSingleton<IInventorDocumentEventService, InventorDocumentEventService>();

            // ✅ ADICIONAR - Serviço de Notificações
            if (runAsService)
            {
                // Modo Serviço: Apenas logs
                services.AddSingleton<IWindowsNotificationService, LogOnlyNotificationService>();
            }
            else
            {
                // Modo Aplicação: Notificações visuais
                services.AddSingleton<IWindowsNotificationService, SimpleNotificationService>();
            }

            services.AddSingleton<WorkSessionService>();
            services.AddSingleton<DocumentProcessingService>();
            services.AddSingleton<IWorkDrivenMonitoringService, WorkDrivenMonitoringService>();

            services.AddHostedService<CompanionWorkerService>();
        })
        .Build();

    await host.RunAsync();
}