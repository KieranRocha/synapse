// Program.cs - CORRIGIDO v3
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

        // Registra os serviços como Singleton (uma única instância para toda a aplicação)

        // ✅ CORREÇÃO APLICADA:
        // 1. Registra a classe concreta 'InventorBomExtractor' como Singleton.
        services.AddSingleton<InventorBomExtractor>();
        // 2. Registra a interface 'IInventorBOMExtractor' e a resolve pegando a instância já registrada da classe concreta.
        //    Isso garante que a mesma instância seja usada em todo o aplicativo, seja injetada pela classe ou pela interface.
        services.AddSingleton<IInventorBOMExtractor>(provider => provider.GetRequiredService<InventorBomExtractor>());

        services.AddSingleton<IInventorConnectionService, InventorConnectionService>();
        services.AddSingleton<IInventorDocumentEventService, InventorDocumentEventService>();
        services.AddSingleton<WorkSessionService>();
        services.AddSingleton<DocumentProcessingService>();
        services.AddSingleton<IWorkDrivenMonitoringService, WorkDrivenMonitoringService>();

        services.AddHostedService<CompanionWorkerService>();
    })
    .Build();

await host.RunAsync();