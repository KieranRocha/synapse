// Em Services/IWorkDrivenMonitoringService.cs
namespace CADCompanion.Agent.Services;

public interface IWorkDrivenMonitoringService
{
    void StartMonitoring();
    void StopMonitoring();
}