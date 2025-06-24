// Program.cs - VERSÃO COMPLETA COM NOTIFICAÇÕES WINDOWS
using CADCompanion.Agent.Configuration;
using CADCompanion.Agent.Services;
using CADCompanion.Agent; // Para InventorBomExtractor
using Serilog;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "CAD Companion Agent";
    })
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
        // ✅ CONFIGURAÇÃO
        services.Configure<CompanionConfiguration>(hostContext.Configuration.GetSection("CompanionConfiguration"));

        // ✅ HTTPCLIENT PARA API
        services.AddHttpClient<IApiCommunicationService, ApiCommunicationService>(client =>
        {
            var serverUrl = hostContext.Configuration["ServerBaseUrl"];
            if (string.IsNullOrEmpty(serverUrl))
            {
                // URL padrão se não estiver configurada
                serverUrl = "http://localhost:5001";
                Console.WriteLine($"⚠️ ServerBaseUrl não configurada, usando padrão: {serverUrl}");
            }
            client.BaseAddress = new Uri(serverUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ✅ SERVIÇOS PRINCIPAIS - ORDEM IMPORTANTE!

        // 1. Serviço de Notificações Windows (NOVO!)
        services.AddSingleton<IWindowsNotificationService, WindowsNotificationService>();

        // 2. Registra a classe concreta 'InventorBomExtractor' como Singleton
        services.AddSingleton<InventorBomExtractor>();

        // 3. Registra a interface usando a mesma instância da classe concreta
        services.AddSingleton<IInventorBOMExtractor>(provider => provider.GetRequiredService<InventorBomExtractor>());

        // 4. Serviços de conexão e eventos do Inventor
        services.AddSingleton<IInventorConnectionService, InventorConnectionService>();
        services.AddSingleton<IInventorDocumentEventService, InventorDocumentEventService>();

        // 5. Serviços de processamento e sessão de trabalho
        services.AddSingleton<WorkSessionService>();
        services.AddSingleton<DocumentProcessingService>();

        // 6. Serviço de monitoramento principal
        services.AddSingleton<IWorkDrivenMonitoringService, WorkDrivenMonitoringService>();

        // 7. Serviço de comunicação com API (será injetado com notificações)
        // Nota: Não precisa registrar novamente pois já foi registrado com HttpClient acima

        // 8. Worker Service principal (deve ser por último)
        services.AddHostedService<CompanionWorkerService>();

        // ✅ CONFIGURAÇÕES ADICIONAIS
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });
    })
    .Build();

// ✅ LOG DE INICIALIZAÇÃO
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 CAD Companion Agent iniciando...");
logger.LogInformation("🔔 Sistema de notificações Windows habilitado");

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "❌ Erro crítico na inicialização do CAD Companion Agent");

    // Tenta mostrar notificação de erro crítico se possível
    try
    {
        var notificationService = host.Services.GetService<IWindowsNotificationService>();
        if (notificationService != null)
        {
            await notificationService.ShowErrorNotificationAsync(
                "Erro Crítico",
                "CAD Companion Agent falhou ao iniciar"
            );
        }
    }
    catch
    {
        // Se falhar aqui, apenas ignora para não mascarar o erro original
    }

    throw;
}
finally
{
    logger.LogInformation("🛑 CAD Companion Agent finalizando...");
    Log.CloseAndFlush();
}