// CADCompanion.Server/Models/PartNumberSequence.cs

using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Server.Models;

public class PartNumberSequence
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string SequenceType { get; set; } = "DEFAULT";
    
    public int LastNumber { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}