// CADCompanion.Server/Models/BomVersion.cs - MANTER COMO STRING
using System.ComponentModel.DataAnnotations.Schema;
using CADCompanion.Shared.Contracts;

namespace CADCompanion.Server.Models;

public class BomVersion
{
    public int Id { get; set; }

    // ✅ MANTER como string por enquanto
    public required string ProjectId { get; set; }
    public required string MachineId { get; set; }

    public required string AssemblyFilePath { get; set; }
    public required string ExtractedBy { get; set; }
    public DateTime ExtractedAt { get; set; }
    public int VersionNumber { get; set; }

    [Column(TypeName = "jsonb")]
    public required List<BomItemDto> Items { get; set; }
}