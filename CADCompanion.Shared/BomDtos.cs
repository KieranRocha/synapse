// CADCompanion.Shared/BomDtos.cs - VERSÃO CORRIGIDA
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
}

// ✅ CLASSE ATUALIZADA - Compatível com BomVersioningService
public class BomComparisonResult
{
    public bool HasChanges { get; set; }

    // ✅ LISTAS SEPARADAS - Como esperado pelo código
    public List<BomItemDto> AddedItems { get; set; } = new();
    public List<BomItemDto> RemovedItems { get; set; } = new();
    public List<ModifiedItemDto> ModifiedItems { get; set; } = new();

    // Contadores para compatibilidade
    public int TotalAdded => AddedItems.Count;
    public int TotalRemoved => RemovedItems.Count;
    public int TotalModified => ModifiedItems.Count;

    public string Summary => $"+{TotalAdded} -{TotalRemoved} ~{TotalModified}";

    // ✅ MANTER COMPATIBILIDADE - Para frontend atual
    public List<BomDiff> Changes { get; set; } = new();
}

// ✅ NOVA CLASSE - Para itens modificados
public class ModifiedItemDto
{
    public required string PartNumber { get; set; }
    public required BomItemDto OldItem { get; set; }
    public required BomItemDto NewItem { get; set; }
    public List<string> Changes { get; set; } = new();
}

// ✅ MANTER CLASSES EXISTENTES - Para compatibilidade frontend
public class BomDiff
{
    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = ""; // "Added", "Removed", "Modified"
    public BomDiffDetail? OldValue { get; set; }
    public BomDiffDetail? NewValue { get; set; }
}

public class BomDiffDetail
{
    public int Quantity { get; set; }
    public string? Description { get; set; }
    public string? StockNumber { get; set; }
}

