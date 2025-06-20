// CADCompanion.Server/Models/Machine.cs
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Server.Models;

public class Machine
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(50)]
    public string? OperationNumber { get; set; } // OP-001, OP-002

    [MaxLength(200)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? FolderPath { get; set; }

    [MaxLength(200)]
    public string? MainAssemblyPath { get; set; } // Arquivo .iam principal

    public MachineStatus Status { get; set; } = MachineStatus.Planning;

    // Relacionamento
    public int ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    // Metadados
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastBomExtraction { get; set; }
    public int TotalBomVersions { get; set; } = 0;

    // Navegação para BOMs
    public virtual ICollection<BomVersion> BomVersions { get; set; } = new List<BomVersion>();
}

public enum MachineStatus
{
    Planning = 1,     // Planejamento
    Design = 2,       // Em projeto
    Review = 3,       // Em revisão
    Manufacturing = 4, // Em fabricação
    Testing = 5,      // Em teste
    Completed = 6     // Finalizada
}