// CADCompanion.Server/Services/BomVersioningService.cs - VERSÃO COMPLETA
using CADCompanion.Server.Data;
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Services;

public class BomVersioningService
{
    private readonly ILogger<BomVersioningService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public BomVersioningService(
        ILogger<BomVersioningService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<BomComparisonResult> CompareBomVersionsAsync(int machineId, int version1, int version2)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            _logger.LogInformation("Comparando versões {Version1} e {Version2} da máquina {MachineId}",
                version1, version2, machineId);

            var bom1 = await context.BomVersions
                .Where(x => x.MachineId == machineId.ToString() && x.VersionNumber == version1)
                .Select(x => x.Items)
                .FirstOrDefaultAsync();

            var bom2 = await context.BomVersions
                .Where(x => x.MachineId == machineId.ToString() && x.VersionNumber == version2)
                .Select(x => x.Items)
                .FirstOrDefaultAsync();

            if (bom1 == null || bom2 == null)
            {
                var missingVersion = bom1 == null ? version1 : version2;
                throw new InvalidOperationException(
                    $"Versão {missingVersion} da BOM não encontrada para máquina {machineId}");
            }

            return CompareBomItems(bom1, bom2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao comparar versões {Version1} e {Version2} da máquina {MachineId}",
                version1, version2, machineId);
            throw;
        }
    }

    public async Task<BomVersion> CreateNewVersionAsync(BomSubmissionDto dto)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var machine = await context.Machines
                .Where(m => m.MainAssemblyPath == dto.AssemblyFilePath)
                .Select(m => new { m.Id, m.ProjectId })
                .FirstOrDefaultAsync();

            if (machine == null)
                throw new InvalidOperationException($"Máquina não encontrada para: {dto.AssemblyFilePath}");

            var maxVersion = await context.BomVersions
                .Where(b => b.MachineId == machine.Id.ToString())
                .MaxAsync(b => (int?)b.VersionNumber) ?? 0;

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

            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar nova versão BOM para {AssemblyPath}", dto.AssemblyFilePath);
            throw;
        }
    }

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

    public bool HasSignificantChanges(List<BomItemDto> oldItems, List<BomItemDto> newItems)
    {
        if (oldItems.Count != newItems.Count) return true;

        var oldDict = oldItems.ToDictionary(i => i.PartNumber);
        return newItems.Any(newItem =>
            !oldDict.TryGetValue(newItem.PartNumber, out var oldItem) ||
            !ItemsAreEqual(oldItem, newItem));
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

    private static List<string> GetItemChanges(BomItemDto oldItem, BomItemDto newItem)
    {
        var changes = new List<string>();

        if (oldItem.Description != newItem.Description)
            changes.Add($"Descrição: '{oldItem.Description}' → '{newItem.Description}'");

        if (Math.Abs(oldItem.Quantity - newItem.Quantity) >= 0.001)
            changes.Add($"Quantidade: {oldItem.Quantity} → {newItem.Quantity}");

        if (oldItem.Material != newItem.Material)
            changes.Add($"Material: '{oldItem.Material}' → '{newItem.Material}'");

        if (Math.Abs((oldItem.Weight ?? 0) - (newItem.Weight ?? 0)) >= 0.001)
            changes.Add($"Peso: {oldItem.Weight:F3} → {newItem.Weight:F3}");

        return changes;
    }
}