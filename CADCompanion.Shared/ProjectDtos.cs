// CADCompanion.Shared/ProjectDtos.cs - CORRIGIDO
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Shared.Contracts;

// DTO para resposta - dados completos do projeto
public class ProjectDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? ContractNumber { get; set; }
    public string? Description { get; set; }
    public string? FolderPath { get; set; }
    public string Status { get; set; } = "Active";
    public string? Client { get; set; }
    public string? ResponsibleEngineer { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? BudgetValue { get; set; }
    public decimal? ActualCost { get; set; }
    public decimal ProgressPercentage { get; set; }
    public int EstimatedHours { get; set; }
    public int ActualHours { get; set; }
    public int MachineCount { get; set; }
    public DateTime? LastActivity { get; set; }
    public int TotalBomVersions { get; set; }

    // Campos calculados para o frontend
    public decimal? BudgetVariance => BudgetValue.HasValue && ActualCost.HasValue
        ? ((ActualCost.Value - BudgetValue.Value) / BudgetValue.Value) * 100
        : null;

    public decimal? HourVariance => EstimatedHours > 0
        ? ((decimal)(ActualHours - EstimatedHours) / EstimatedHours) * 100
        : null;
}

// DTO para criação de projeto
public class CreateProjectDto
{
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(50)]
    public string? ContractNumber { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? FolderPath { get; set; }

    [MaxLength(100)]
    public string? Client { get; set; }

    [MaxLength(50)]
    public string? ResponsibleEngineer { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? BudgetValue { get; set; }
    public int EstimatedHours { get; set; } = 0;

    // Lista de máquinas iniciais (opcional)
    public List<string>? InitialMachines { get; set; }
}

// DTO para atualização de projeto
public class UpdateProjectDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(50)]
    public string? ContractNumber { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? FolderPath { get; set; }

    public string? Status { get; set; }

    [MaxLength(100)]
    public string? Client { get; set; }

    [MaxLength(50)]
    public string? ResponsibleEngineer { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? BudgetValue { get; set; }
    public decimal? ActualCost { get; set; }
    public decimal? ProgressPercentage { get; set; }
    public int? EstimatedHours { get; set; }
    public int? ActualHours { get; set; }
}

// DTO resumido para listagens
public class ProjectSummaryDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? ContractNumber { get; set; }
    public string Status { get; set; } = "Active";
    public string? Client { get; set; }
    public decimal ProgressPercentage { get; set; }
    public int MachineCount { get; set; }
    public DateTime? LastActivity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EndDate { get; set; } // Campo adicionado

    // Indicadores visuais para o frontend
    public bool IsOverdue => EndDate.HasValue && EndDate.Value < DateTime.UtcNow && Status != "Completed";
    public bool IsActive => Status == "Active";
    public string StatusColor => Status switch
    {
        "Active" => "green",
        "Planning" => "blue",
        "OnHold" => "yellow",
        "Review" => "orange",
        "Completed" => "gray",
        "Cancelled" => "red",
        _ => "gray"
    };
}