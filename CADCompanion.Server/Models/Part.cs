// CADCompanion.Server/Models/Part.cs
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Server.Models;

public class Part
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string PartNumber { get; set; } = string.Empty; // 000001, 000002, etc.
    
    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string? Category { get; set; } // bearing, fastener, motor, etc.
    
    [StringLength(100)]
    public string? Material { get; set; }
    
    public decimal? Weight { get; set; } // kg
    
    public decimal? Cost { get; set; } // R$
    
    [StringLength(100)]
    public string? Supplier { get; set; }
    
    [StringLength(100)]
    public string? Manufacturer { get; set; }
    
    [StringLength(100)]
    public string? ManufacturerPartNumber { get; set; }
    
    public string? DatasheetUrl { get; set; }
    
    public string? ImageUrl { get; set; }
    
    // Propriedades customizadas vindas do CAD (JSON)
    public Dictionary<string, object>? CustomProperties { get; set; }
    
    public bool IsStandardPart { get; set; } = false;
    
    public PartStatus Status { get; set; } = PartStatus.AutoCreated;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navegação: onde esta peça é usada
    public virtual ICollection<BomPartUsage> BomUsages { get; set; } = new List<BomPartUsage>();
}

public enum PartStatus
{
    AutoCreated,    // Criada automaticamente pelo sistema
    InReview,       // Sendo revisada por usuário
    Approved,       // Aprovada e completa
    Obsolete        // Não mais em uso
}