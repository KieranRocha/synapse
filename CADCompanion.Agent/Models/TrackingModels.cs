namespace CADCompanion.Agent.Models;

public class MachineTrackingInfo
{
    public int MachineId { get; set; }
    public int ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public DateTime? LastSync { get; set; }
    public bool IsValid => MachineId > 0 && ProjectId > 0;
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ProjectName { get; set; }
    public string? MachineName { get; set; }
    public int ActualProjectId { get; set; }
    public string? ActualProjectName { get; set; }
    public string? ErrorMessage { get; set; }
}