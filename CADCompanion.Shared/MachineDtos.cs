// CADCompanion.Shared/MachineDtos.cs - NOVO ARQUIVO
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Shared.Contracts;

// DTO para resposta completa da máquina
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

    // Lista de versões de BOM (resumo)
    public List<BomVersionSummaryDto> BomVersions { get; set; } = new();
}

// DTO resumido para listagens


public class MachineSummaryDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? OperationNumber { get; set; }
    public string Status { get; set; } = "Planning";
    public int TotalBomVersions { get; set; }
    public DateTime? LastBomExtraction { get; set; }

    // ✅ ADICIONAR StatusColor para compatibilidade com frontend
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

// DTO para criação de máquina
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

    // ✅ IMPORTANTE: ProjectId é setado automaticamente pelo controller
    public int ProjectId { get; set; }
}

// DTO para atualização de máquina
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

// DTO resumido para versões de BOM
public class BomVersionSummaryDto
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ExtractedBy { get; set; }
    public int ItemCount { get; set; }
    public DateTime ExtractedAt { get; set; }
    public List<BomItemDto> Items { get; set; } = new();

}
public class UpdateStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? CurrentFile { get; set; }
}