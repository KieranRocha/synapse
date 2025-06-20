// CADCompanion.Shared/MachineDtos.cs - DTOs COMPLETOS
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Shared.Contracts;

// DTO para resposta - dados completos da máquina
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

    // Navegação
    public List<BomVersionSummaryDto>? BomVersions { get; set; }
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

    [Required]
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

// DTO resumido para listagens
public class MachineSummaryDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? OperationNumber { get; set; }
    public string Status { get; set; } = "Planning";
    public int TotalBomVersions { get; set; }
    public DateTime? LastBomExtraction { get; set; }
}

// DTO para versões de BOM (resumido)
public class BomVersionSummaryDto
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public DateTime ExtractedAt { get; set; }
    public string ExtractedBy { get; set; } = string.Empty;
    public int TotalItems { get; set; }
}