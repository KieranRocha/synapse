// CADCompanion.Server/Services/BomVersioningService.cs - ATUALIZADO
using CADCompanion.Server.Data;
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Services;

public class BomVersioningService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPartService _partService; // ✅ NOVO
    private readonly ILogger<BomVersioningService> _logger;

    public BomVersioningService(
        IServiceScopeFactory scopeFactory,
        IPartService partService, // ✅ NOVO
        ILogger<BomVersioningService> logger)
    {
        _scopeFactory = scopeFactory;
        _partService = partService; // ✅ NOVO
        _logger = logger;
    }

    // ✅ MÉTODO PRINCIPAL ATUALIZADO  
    public async Task<BomVersion> CreateBomVersion(BomSubmissionDto dto)
{
    using var scope = _scopeFactory.CreateScope();
    using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    using var transaction = await context.Database.BeginTransactionAsync();

    try
    {
        _logger.LogInformation("🔄 Iniciando criação de BOM version para {AssemblyPath}", dto.AssemblyFilePath);

        // Buscar máquina pelo MachineId ou pelo ExtractedBy
        var machine = await context.Machines
            .FirstOrDefaultAsync(m => m.Id.ToString() == dto.MachineId || m.Name == dto.ExtractedBy);

        if (machine == null)
        {
            _logger.LogWarning("❌ Máquina não encontrada para MachineId={MachineId} ou ExtractedBy={ExtractedBy}", 
                dto.MachineId, dto.ExtractedBy);
            throw new ArgumentException($"Máquina não encontrada para ID '{dto.MachineId}' ou nome '{dto.ExtractedBy}'");
        }

        _logger.LogInformation("✅ Máquina encontrada: {MachineName} (ID: {MachineId})", machine.Name, machine.Id);

        // Buscar última versão para esta máquina
        var maxVersion = await context.BomVersions
            .Where(bv => bv.MachineId == machine.Id.ToString())
            .MaxAsync(bv => (int?)bv.VersionNumber) ?? 0;

        // ✅ CRIAR NOVA VERSÃO BOM
        var newVersion = new BomVersion
        {
            ProjectId = machine.ProjectId.ToString(),
            MachineId = machine.Id.ToString(),
            AssemblyFilePath = dto.AssemblyFilePath,
            ExtractedBy = dto.ExtractedBy,
            ExtractedAt = dto.ExtractedAt.ToUniversalTime(),
            VersionNumber = maxVersion + 1,
            Items = dto.Items ?? new List<BomItemDto>()
        };

        context.BomVersions.Add(newVersion);
        await context.SaveChangesAsync(); // ✅ Isso gera o ID

        _logger.LogInformation("✅ Nova versão BOM criada: V{Version} (ID: {BomVersionId}) para máquina {MachineId}", 
            newVersion.VersionNumber, newVersion.Id, machine.Id);

        // ✅ AGORA SINCRONIZAR COM CATÁLOGO DE PEÇAS NO MESMO CONTEXTO
        await SyncPartsFromBomInContext(context, newVersion.Id, dto.Items ?? new List<BomItemDto>());

        await transaction.CommitAsync();

        _logger.LogInformation("✅ BOM V{Version} criado e sincronizado com catálogo de peças", 
            newVersion.VersionNumber);

        return newVersion;
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "❌ Erro ao criar nova versão BOM para {AssemblyPath}", dto.AssemblyFilePath);
        throw;
    }
}

// ✅ NOVO MÉTODO: Sincronização usando o contexto correto
private async Task SyncPartsFromBomInContext(AppDbContext context, int bomVersionId, List<BomItemDto> bomItems)
{
    _logger.LogInformation("🔧 Sincronizando {Count} itens do BOM {BomVersionId} no mesmo contexto", 
        bomItems.Count, bomVersionId);

    if (bomItems == null || bomItems.Count == 0)
    {
        _logger.LogWarning("⚠️ Lista de BOM items está vazia, nada para sincronizar");
        return;
    }

    // ✅ GARANTIR QUE SEQUÊNCIA DE PART NUMBER EXISTE
    await EnsurePartNumberSequenceExists(context);

    int successCount = 0;
    int errorCount = 0;

    foreach (var bomItem in bomItems)
    {
        try
        {
            if (!IsValidBomItem(bomItem))
                continue;

            // ✅ PROCESSAR ITEM NO MESMO CONTEXTO
            await ProcessBomItemInContext(context, bomVersionId, bomItem);
            successCount++;

            _logger.LogDebug("✅ Item processado: {PartNumber}", bomItem.PartNumber);
        }
        catch (Exception ex)
        {
            errorCount++;
            _logger.LogWarning(ex, "⚠️ Erro ao processar item {PartNumber}, continuando...", 
                bomItem.PartNumber ?? "UNKNOWN");
        }
    }

    _logger.LogInformation("✅ Sincronização concluída. Sucesso: {Success}, Erros: {Errors}", 
        successCount, errorCount);
}

private bool IsValidBomItem(BomItemDto item)
{
    return !string.IsNullOrWhiteSpace(item.PartNumber) && item.Quantity > 0;
}

private async Task ProcessBomItemInContext(AppDbContext context, int bomVersionId, BomItemDto bomItem)
{
    // 1. Buscar peça existente
    var existingPart = await context.Parts
        .FirstOrDefaultAsync(p => p.PartNumber == bomItem.PartNumber);

    string partNumber;
    
    if (existingPart != null)
    {
        partNumber = existingPart.PartNumber;
        _logger.LogDebug("🔍 Peça existente encontrada: {PartNumber}", partNumber);
    }
    else
    {
        // 2. Criar nova peça com part number sequencial
        partNumber = await _partService.GetNextPartNumber();
        
        var newPart = new Part
        {
            PartNumber = partNumber,
            Description = bomItem.Description ?? "Sem descrição",
            Material = bomItem.Material,
            Weight = bomItem.Weight.HasValue ? (decimal)bomItem.Weight.Value : null,
            Category = ClassifyPart(bomItem.Description ?? ""),
            Status = PartStatus.AutoCreated,
            IsStandardPart = IsStandardPart(bomItem.Description ?? ""),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Parts.Add(newPart);
        await context.SaveChangesAsync();

        _logger.LogDebug("✅ Nova peça criada: {OriginalPN} -> {NewPartNumber}", 
            bomItem.PartNumber, partNumber);
    }

    // 3. Criar/atualizar relacionamento BOM -> Part
    var existingUsage = await context.BomPartUsages
        .FirstOrDefaultAsync(u => u.BomVersionId == bomVersionId && 
                                u.PartNumber == partNumber);

    if (existingUsage == null)
    {
        var newUsage = new BomPartUsage
        {
            BomVersionId = bomVersionId,
            PartNumber = partNumber,
            Quantity = bomItem.Quantity,
            Level = bomItem.Level > 0 ? bomItem.Level : 1,
            CreatedAt = DateTime.UtcNow
        };

        context.BomPartUsages.Add(newUsage);
    }
    else
    {
        existingUsage.Quantity = bomItem.Quantity;
        existingUsage.Level = bomItem.Level > 0 ? bomItem.Level : 1;
    }

    await context.SaveChangesAsync();
}
private string ClassifyPart(string description)
{
    if (string.IsNullOrEmpty(description))
        return "Unknown";

    var desc = description.ToLower();
    
    if (desc.Contains("screw") || desc.Contains("bolt") || desc.Contains("nut"))
        return "Fastener";
    if (desc.Contains("bearing") || desc.Contains("rolamento"))
        return "Bearing";
    if (desc.Contains("gasket") || desc.Contains("seal") || desc.Contains("vedação"))
        return "Seal";
    if (desc.Contains("motor") || desc.Contains("cylinder"))
        return "Actuator";
    if (desc.Contains("valve") || desc.Contains("válvula"))
        return "Valve";
    if (desc.Contains("sensor"))
        return "Sensor";
    
    return "Mechanical";
}

private bool IsStandardPart(string description)
{
    if (string.IsNullOrEmpty(description))
        return false;

    var desc = description.ToLower();
    
    return desc.Contains("din") || 
           desc.Contains("iso") || 
           desc.Contains("ansi") ||
           desc.Contains("skf") ||
           desc.Contains("smc") ||
           desc.Contains("parker") ||
           desc.Contains("festo") ||
           desc.Contains("m6") || desc.Contains("m8") || desc.Contains("m10") ||
           desc.Contains("screw") || 
           desc.Contains("bolt") || 
           desc.Contains("nut");
}
private async Task EnsurePartNumberSequenceExists(AppDbContext context)
{
    var sequence = await context.PartNumberSequences
        .FirstOrDefaultAsync(s => s.SequenceType == "DEFAULT");

    if (sequence == null)
    {
        _logger.LogInformation("📝 Criando sequência de part number inicial");
        
        var newSequence = new PartNumberSequence
        {
            SequenceType = "DEFAULT",
            LastNumber = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.PartNumberSequences.Add(newSequence);
        await context.SaveChangesAsync();
    }
}
    // ✅ MÉTODOS EXISTENTES (não mudam)
    public async Task<List<BomVersionSummaryDto>> GetMachineVersionsAsync(int machineId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await context.BomVersions
                .Where(bv => bv.MachineId == machineId.ToString())
                .OrderByDescending(bv => bv.VersionNumber)
                .Select(bv => new BomVersionSummaryDto
                {
                    Id = bv.Id,
                    VersionNumber = bv.VersionNumber,
                    ExtractedAt = bv.ExtractedAt,
                    ExtractedBy = bv.ExtractedBy,
                    ItemCount = bv.Items.Count
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar versões da máquina {MachineId}", machineId);
            throw;
        }
    }

    public async Task<BomComparisonResult> CompareBomVersions(int versionId1, int versionId2)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var version1 = await context.BomVersions.FindAsync(versionId1);
            var version2 = await context.BomVersions.FindAsync(versionId2);

            if (version1 == null || version2 == null)
                throw new ArgumentException("Uma ou ambas as versões não foram encontradas");

            return CompareBomItems(version1.Items, version2.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao comparar versões {Version1} e {Version2}", versionId1, versionId2);
            throw;
        }
    }

    // ✅ MÉTODOS HELPERS EXISTENTES
    public bool HasSignificantChanges(List<BomItemDto> oldItems, List<BomItemDto> newItems)
    {
        if (oldItems.Count != newItems.Count) return true;

        var oldDict = oldItems.ToDictionary(i => i.PartNumber);
        return newItems.Any(newItem =>
            !oldDict.TryGetValue(newItem.PartNumber, out var oldItem) ||
            !ItemsAreEqual(oldItem, newItem));
    }

    private async Task<BomVersion?> GetLastBomVersionByPath(AppDbContext context, string assemblyFilePath)
    {
        return await context.BomVersions
            .Where(bv => bv.AssemblyFilePath == assemblyFilePath)
            .OrderByDescending(bv => bv.VersionNumber)
            .FirstOrDefaultAsync();
    }

    private BomComparisonResult CompareBomItems(List<BomItemDto> oldItems, List<BomItemDto> newItems)
    {
        var result = new BomComparisonResult();
        var oldItemsDict = oldItems.ToDictionary(item => item.PartNumber, item => item);
        var newItemsDict = newItems.ToDictionary(item => item.PartNumber, item => item);

        // Itens removidos
        foreach (var oldItem in oldItems.Where(item => !newItemsDict.ContainsKey(item.PartNumber)))
        {
            result.RemovedItems.Add(oldItem);
            result.Changes.Add(new BomDiff
            {
                PartNumber = oldItem.PartNumber,
                Description = oldItem.Description ?? "",
                Type = "Removed",
                OldValue = new BomDiffDetail
                {
                    Quantity = oldItem.Quantity,
                    Description = oldItem.Description,
                    StockNumber = oldItem.StockNumber
                }
            });
        }

        // Itens adicionados
        foreach (var newItem in newItems.Where(item => !oldItemsDict.ContainsKey(item.PartNumber)))
        {
            result.AddedItems.Add(newItem);
            result.Changes.Add(new BomDiff
            {
                PartNumber = newItem.PartNumber,
                Description = newItem.Description ?? "",
                Type = "Added",
                NewValue = new BomDiffDetail
                {
                    Quantity = newItem.Quantity,
                    Description = newItem.Description,
                    StockNumber = newItem.StockNumber
                }
            });
        }

        // Itens modificados
        foreach (var newItem in newItems.Where(item => oldItemsDict.ContainsKey(item.PartNumber)))
        {
            var oldItem = oldItemsDict[newItem.PartNumber];
            if (!ItemsAreEqual(oldItem, newItem))
            {
                result.ModifiedItems.Add(new ModifiedItemDto
                {
                    PartNumber = newItem.PartNumber,
                    OldItem = oldItem,
                    NewItem = newItem,
                    Changes = GetItemChanges(oldItem, newItem)
                });

                result.Changes.Add(new BomDiff
                {
                    PartNumber = newItem.PartNumber,
                    Description = newItem.Description ?? "",
                    Type = "Modified",
                    OldValue = new BomDiffDetail
                    {
                        Quantity = oldItem.Quantity,
                        Description = oldItem.Description,
                        StockNumber = oldItem.StockNumber
                    },
                    NewValue = new BomDiffDetail
                    {
                        Quantity = newItem.Quantity,
                        Description = newItem.Description,
                        StockNumber = newItem.StockNumber
                    }
                });
            }
        }

        result.HasChanges = result.AddedItems.Any() || result.RemovedItems.Any() || result.ModifiedItems.Any();
        return result;
    }

    private static bool ItemsAreEqual(BomItemDto item1, BomItemDto item2)
    {
        return item1.PartNumber == item2.PartNumber &&
               item1.Description == item2.Description &&
               Math.Abs(item1.Quantity - item2.Quantity) < 0.001 &&
               item1.Material == item2.Material &&
               Math.Abs((item1.Weight ?? 0) - (item2.Weight ?? 0)) < 0.001;
    }

    private List<string> GetItemChanges(BomItemDto oldItem, BomItemDto newItem)
    {
        var changes = new List<string>();

        if (Math.Abs(oldItem.Quantity - newItem.Quantity) > 0.001)
            changes.Add($"Quantidade: {oldItem.Quantity} → {newItem.Quantity}");

        if (oldItem.Description != newItem.Description)
            changes.Add($"Descrição: {oldItem.Description} → {newItem.Description}");

        if (oldItem.Material != newItem.Material)
            changes.Add($"Material: {oldItem.Material} → {newItem.Material}");

        return changes;
    }
}