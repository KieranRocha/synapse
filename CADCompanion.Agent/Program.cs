// Program.cs - VERSÃO COM DEPURAÇÃO (CORRIGIDO)
using CADCompanion.Agent;
using CADCompanion.Agent.Configuration;
using CADCompanion.Agent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows.Forms;

try
{
    // Atributo necessário para aplicações com UI, como o NotifyIcon
    [STAThread]
    static async Task Main(string[] args)
    {
        Console.WriteLine("DEBUG: Iniciando Main()...");

        // Determina se o app deve rodar como serviço ou como aplicação de console/bandeja
        bool runAsService = args.Contains("--service") || !Environment.UserInteractive;
        Console.WriteLine($"DEBUG: Modo interativo? {!runAsService}");

        // Apenas inicializa componentes de UI se não estiver rodando como serviço
        if (!runAsService)
        {
            Console.WriteLine("DEBUG: Habilitando estilos visuais do Windows Forms...");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }

        Console.WriteLine("DEBUG: Criando HostBuilder...");
        var hostBuilder = Host.CreateDefaultBuilder(args);

        // Habilita o modo "Serviço do Windows" se aplicável
        if (runAsService)
        {
            Console.WriteLine("DEBUG: Configurando para rodar como Serviço do Windows.");
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
                Console.WriteLine("DEBUG: Configurando serviços (ConfigureServices)...");
                services.Configure<CompanionConfiguration>(hostContext.Configuration.GetSection("CompanionConfiguration"));

                services.AddHttpClient<IApiCommunicationService, ApiCommunicationService>(client =>
                {
                    var serverUrl = hostContext.Configuration["CompanionConfiguration:Settings:ApiBaseUrl"] ?? "http://localhost:5001";
                    client.BaseAddress = new Uri(serverUrl);
                });

                // ✅ CORREÇÃO: Registra tanto a classe concreta quanto a interface.
                // Isso garante que o serviço possa ser injetado onde for necessário.
                services.AddSingleton<InventorBomExtractor>();
                services.AddSingleton<IInventorBOMExtractor>(provider => provider.GetRequiredService<InventorBomExtractor>());

                services.AddSingleton<IInventorConnectionService, InventorConnectionService>();
                services.AddSingleton<IInventorDocumentEventService, InventorDocumentEventService>();

                if (runAsService)
                {
                    Console.WriteLine("DEBUG: Registrando LogOnlyNotificationService.");
                    services.AddSingleton<IWindowsNotificationService, LogOnlyNotificationService>();
                }
                else
                {
                    Console.WriteLine("DEBUG: Registrando WindowsNotificationService (para o ícone da bandeja).");
                    services.AddSingleton<IWindowsNotificationService, WindowsNotificationService>();
                }

                services.AddSingleton<WorkSessionService>();
                services.AddSingleton<DocumentProcessingService>();
                services.AddSingleton<IWorkDrivenMonitoringService, WorkDrivenMonitoringService>();
                services.AddHostedService<CompanionWorkerService>();
                Console.WriteLine("DEBUG: Configuração de serviços completa.");
            })
            .Build();

        Console.WriteLine("DEBUG: Host construído com sucesso.");

        if (runAsService)
        {
            Console.WriteLine("DEBUG: Iniciando host em modo de serviço...");
            await host.RunAsync();
        }
        else
        {
            using (host)
            {
                Console.WriteLine("DEBUG: Iniciando host em modo interativo...");
                await host.StartAsync();

                Console.WriteLine("DEBUG: Host iniciado. Chamando Application.Run() para criar o ícone...");
                Application.Run();

                Console.WriteLine("DEBUG: Application.Run() finalizado (usuário clicou em Sair). Parando o host...");
                await host.StopAsync();
            }
        }
    }

    // A chamada ao Main precisa estar dentro de um try/catch para a depuração funcionar
    await Main(args);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n!!!!!!!!!! ERRO CRÍTICO NA INICIALIZAÇÃO !!!!!!!!!!\n");
    Console.WriteLine(ex.ToString());
    Console.ResetColor();
    File.WriteAllText("crash.log", ex.ToString());
    Console.WriteLine("\nUm erro crítico ocorreu. Detalhes foram salvos no arquivo 'crash.log'.");
    Console.WriteLine("Pressione qualquer tecla para sair...");
    Console.ReadKey();
}