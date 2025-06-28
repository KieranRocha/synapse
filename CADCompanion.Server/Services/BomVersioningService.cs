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
            // Buscar máquina pelo caminho do assembly
            var machine = await context.Machines
                .FirstOrDefaultAsync(m => m.MainAssemblyPath == dto.AssemblyFilePath);
                
            if (machine == null)
                throw new ArgumentException($"Máquina não encontrada para o arquivo {dto.AssemblyFilePath}");

            // Verificar se há mudanças significativas
            var lastVersion = await GetLastBomVersionByPath(context, dto.AssemblyFilePath);
            if (lastVersion != null && !HasSignificantChanges(lastVersion.Items, dto.Items))
            {
                _logger.LogInformation("Nenhuma mudança significativa detectada para {AssemblyPath}", dto.AssemblyFilePath);
                return lastVersion;
            }

            // Calcular próximo número de versão
            var maxVersion = await context.BomVersions
                .Where(bv => bv.AssemblyFilePath == dto.AssemblyFilePath)
                .MaxAsync(bv => (int?)bv.VersionNumber) ?? 0;

            // ✅ CRIAR NOVA VERSÃO BOM (como sempre)
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
            await context.SaveChangesAsync();

            _logger.LogInformation("Nova versão BOM criada: V{Version} para máquina {MachineId}",
                newVersion.VersionNumber, machine.Id);

            // ✅ NOVO: SINCRONIZAR COM CATÁLOGO DE PEÇAS
            await _partService.SyncPartsFromBom(newVersion.Id, dto.Items ?? new List<BomItemDto>());

            await transaction.CommitAsync();

            _logger.LogInformation("BOM V{Version} criado e sincronizado com catálogo de peças", 
                newVersion.VersionNumber);

            return newVersion;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erro ao criar nova versão BOM para {AssemblyPath}", dto.AssemblyFilePath);
            throw;
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