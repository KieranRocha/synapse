public class SystemHealthService : ISystemHealthService
{
    private readonly ILogger<SystemHealthService> _logger;
    private readonly AppDbContext _context;

    public SystemHealthService(ILogger<SystemHealthService> logger, AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<SystemStatusDto> GetSystemStatusAsync()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var services = await CheckServicesAsync();

            var status = new SystemStatusDto
            {
                Status = DetermineOverallStatus(services),
                CpuUsage = GetCpuUsage(),
                MemoryUsage = (process.WorkingSet64 / (1024.0 * 1024.0)),
                DiskUsage = GetDiskUsage(),
                ActiveConnections = 0, // TODO: Implementar
                Uptime = DateTime.UtcNow - process.StartTime,
                Services = services,
                LastHealthCheck = DateTime.UtcNow
            };

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar status do sistema");
            throw;
        }
    }

    private async Task<List<ServiceStatusDto>> CheckServicesAsync()
    {
        var services = new List<ServiceStatusDto>();

        // Database
        var dbStatus = await CheckDatabaseAsync();
        services.Add(dbStatus);

        // TODO: Adicionar outros serviços

        return services;
    }

    private async Task<ServiceStatusDto> CheckDatabaseAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _context.Database.CanConnectAsync();
            stopwatch.Stop();

            return new ServiceStatusDto
            {
                Name = "Database",
                Status = "healthy",
                LastCheck = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new ServiceStatusDto
            {
                Name = "Database",
                Status = "unhealthy",
                ErrorMessage = ex.Message,
                LastCheck = DateTime.UtcNow,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    private string DetermineOverallStatus(List<ServiceStatusDto> services)
    {
        if (services.Any(s => s.Status == "unhealthy"))
            return "critical";

        if (services.Any(s => s.Status == "degraded"))
            return "warning";

        return "healthy";
    }

    private double GetCpuUsage()
    {
        // TODO: Implementar cálculo real de CPU
        return 0.0;
    }

    private double GetDiskUsage()
    {
        try
        {
            var drive = new DriveInfo(Environment.CurrentDirectory);
            var usedSpace = drive.TotalSize - drive.AvailableFreeSpace;
            return (double)usedSpace / drive.TotalSize * 100;
        }
        catch
        {
            return 0.0;
        }
    }
}
