// CADCompanion.Server/Models/Project.cs
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Server.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(50)]
    public string? ContractNumber { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? FolderPath { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    [MaxLength(100)]
    public string? Client { get; set; }

    [MaxLength(50)]
    public string? ResponsibleEngineer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Campos financeiros - críticos para ROI
    public decimal? BudgetValue { get; set; }
    public decimal? ActualCost { get; set; }

    // Campos de progresso - KPIs industriais
    public decimal ProgressPercentage { get; set; } = 0;
    public int EstimatedHours { get; set; } = 0;
    public int ActualHours { get; set; } = 0;

    // Metadados técnicos
    public int MachineCount { get; set; } = 0;
    public DateTime? LastActivity { get; set; }
    public int TotalBomVersions { get; set; } = 0;

    // Navegação para BOMs
    public virtual ICollection<BomVersion> BomVersions { get; set; } = new List<BomVersion>();
}

public enum ProjectStatus
{
    Planning = 1,      // Planejamento
    Active = 2,        // Em execução
    OnHold = 3,        // Pausado
    Review = 4,        // Em revisão
    Completed = 5,     // Concluído
    Cancelled = 6      // Cancelado
}