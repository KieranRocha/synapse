// Em CADCompanion.Server/Models/BomVersion.cs
using System.ComponentModel.DataAnnotations.Schema;
using CADCompanion.Shared.Contracts; // Referencia os DTOs!

namespace CADCompanion.Server.Models;

public class BomVersion
{
    public int Id { get; set; }
    public required string ProjectId { get; set; }
    public required string MachineId { get; set; }
    public required string AssemblyFilePath { get; set; }
    public required string ExtractedBy { get; set; }
    public DateTime ExtractedAt { get; set; }
    public int VersionNumber { get; set; }

    // Armazena a lista de itens da BOM como um campo JSONB no PostgreSQL
    [Column(TypeName = "jsonb")]
    public required List<BomItemDto> Items { get; set; }
}