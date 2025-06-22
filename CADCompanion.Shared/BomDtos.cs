// Em CADCompanion.Shared/BomDtos.cs
namespace CADCompanion.Shared.Contracts;

public class BomSubmissionDto
{
    public string? ProjectId { get; set; }
    public string? MachineId { get; set; }
    public required string AssemblyFilePath { get; set; }
    public required List<BomItemDto> Items { get; set; }
    public required string ExtractedBy { get; set; }
    public DateTime ExtractedAt { get; set; }
}

public class BomItemDto
{
    public required string PartNumber { get; set; }
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public string? StockNumber { get; set; }
    public int Level { get; set; }
    public bool IsAssembly { get; set; }
    public string? Material { get; set; }
    public double? Weight { get; set; }
    // Adicione outras propriedades que você extrai e quer enviar
}