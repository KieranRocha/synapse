// CADCompanion.Shared/MachineDtos.cs - CORRIGIDO
using System.ComponentModel.DataAnnotations; // ← ADICIONAR ESTA LINHA

namespace CADCompanion.Shared.Contracts;

public class MachineDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? OperationNumber { get; set; }
    public string? Description { get; set; }
    public string? FolderPath { get; set; }
    public string? MainAssemblyPath { get; set; }
    public string Status { get; set; } = "Planning";
    public int ProjectId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastBomExtraction { get; set; }
    public int TotalBomVersions { get; set; }
}

public class CreateMachineDto
{
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(50)]
    public string? OperationNumber { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? FolderPath { get; set; }

    [MaxLength(200)]
    public string? MainAssemblyPath { get; set; }
}

public class UpdateMachineDto
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(50)]
    public string? OperationNumber { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? FolderPath { get; set; }

    [MaxLength(200)]
    public string? MainAssemblyPath { get; set; }

    public string? Status { get; set; }
}

public class MachineSummaryDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? OperationNumber { get; set; }
    public string Status { get; set; } = "Planning";
    public int TotalBomVersions { get; set; }
    public DateTime? LastBomExtraction { get; set; }
    public string StatusColor => Status switch
    {
        "Planning" => "blue",
        "Design" => "yellow",
        "Review" => "orange",
        "Manufacturing" => "green",
        "Testing" => "purple",
        "Completed" => "gray",
        _ => "gray"
    };
}