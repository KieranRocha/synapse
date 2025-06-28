// CADCompanion.Server/Models/BomPartUsage.cs
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Server.Models;

public class BomPartUsage
{
    public int Id { get; set; }
    
    // FK para BomVersion
    public int BomVersionId { get; set; }
    public virtual BomVersion BomVersion { get; set; } = null!;
    
    // FK para Part (por PartNumber, não ID)
    [Required]
    [StringLength(6)]
    public string PartNumber { get; set; } = string.Empty;
    public virtual Part Part { get; set; } = null!;
    
    // Dados do uso desta peça neste BOM
    public decimal Quantity { get; set; }
    
    public int Level { get; set; } = 1; // Nível na hierarquia do BOM
    
    [StringLength(50)]
    public string? ParentPartNumber { get; set; } // Part number do componente pai
    
    [StringLength(100)]
    public string? ReferenceDesignator { get; set; } // P1, P2, C1, etc.
    
    public bool IsOptional { get; set; } = false;
    
    [StringLength(200)]
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}