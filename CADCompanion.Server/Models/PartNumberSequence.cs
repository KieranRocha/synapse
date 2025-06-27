// CADCompanion.Server/Models/PartNumberSequence.cs
using System.ComponentModel.DataAnnotations;

namespace CADCompanion.Server.Models;

public class PartNumberSequence
{
    public int Id { get; set; }
    
    [Required]
    public int LastNumber { get; set; } = 0; // Último número usado (ex: 847 para gerar 000848)
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Para futuro: diferentes sequências por tipo/categoria se necessário
    [StringLength(50)]
    public string SequenceType { get; set; } = "DEFAULT"; // Por enquanto só uma sequência
}