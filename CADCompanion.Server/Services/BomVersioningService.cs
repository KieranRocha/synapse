// CADCompanion.Server/Services/BomVersioningService.cs - CORRIGIDO E MELHORADO
using CADCompanion.Server.Data;
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CADCompanion.Server.Services;

// A classe agora é uma única definição. Removi a classe parcial aninhada.
public class BomVersioningService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BomVersioningService> _logger;

    public BomVersioningService(AppDbContext context, ILogger<BomVersioningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BomVersion> CreateNewVersionAsync(BomSubmissionDto dto)
    {
        try
        {
            // Buscar máquina pelo assembly path
            var machine = await _context.Machines
                .FirstOrDefaultAsync(m => m.MainAssemblyPath == dto.AssemblyFilePath);

            if (machine == null)
            {
                var folderPath = Path.GetDirectoryName(dto.AssemblyFilePath);
                // A busca por folderPath pode retornar múltiplas máquinas se não for exata.
                // Considere uma lógica mais robusta se os diretórios puderem se sobrepor.
                machine = await _context.Machines
                    .FirstOrDefaultAsync(m => m.FolderPath != null && folderPath!.Contains(m.FolderPath));
            }

            if (machine == null)
            {
                throw new InvalidOperationException($"Máquina não encontrada para o caminho: {dto.AssemblyFilePath}");
            }

            // Próximo número de versão
            var lastVersion = await _context.BomVersions
                // ✅ MELHORIA: Comparação direta com o ID numérico (machine.Id) ao invés de string.
                // Isso requer que a propriedade BomVersion.MachineId seja do mesmo tipo que Machine.Id (ex: int).
                .Where(bv => bv.MachineId == machine.Id.ToString())
                .OrderByDescending(bv => bv.VersionNumber)
                .FirstOrDefaultAsync();

            var newVersionNumber = (lastVersion?.VersionNumber ?? 0) + 1;

            // Criar nova versão
            var newVersion = new BomVersion
            {
                // ✅ MELHORIA: Atribuição direta dos Ids.
                ProjectId = machine.ProjectId.ToString(),
                MachineId = machine.Id.ToString(),
                AssemblyFilePath = dto.AssemblyFilePath,
                ExtractedBy = dto.ExtractedBy,
                ExtractedAt = dto.ExtractedAt.ToUniversalTime(),
                VersionNumber = newVersionNumber,
                Items = dto.Items ?? new List<BomItemDto>()
            };

            _context.BomVersions.Add(newVersion);
            await _context.SaveChangesAsync();

            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar uma nova versão da BOM para {AssemblyPath}", dto.AssemblyFilePath);
            throw;
        }
    }

    // Os métodos a seguir foram movidos da classe parcial aninhada para aqui.

    /// <summary>
    /// Compara duas versões de BOM de uma máquina específica.
    /// </summary>
    public async Task<BomComparisonResult> CompareBomVersionsAsync(int machineId, int version1, int version2)
    {
        try
        {
            var bom1Task = _context.BomVersions
                .Where(x => x.MachineId == machineId.ToString() && x.VersionNumber == version1)
                .Select(x => x.Items)
                .FirstOrDefaultAsync();

            var bom2Task = _context.BomVersions
                .Where(x => x.MachineId == machineId.ToString() && x.VersionNumber == version2)
                .Select(x => x.Items)
                .FirstOrDefaultAsync();

            // Executa as buscas em paralelo para mais eficiência
            await Task.WhenAll(bom1Task, bom2Task);

            var bom1 = await bom1Task;
            var bom2 = await bom2Task;

            if (bom1 == null || bom2 == null)
            {
                // Fornece uma mensagem de erro mais clara
                var notFoundVersion = bom1 == null ? version1 : version2;
                throw new InvalidOperationException($"A versão {notFoundVersion} da BOM para a máquina ID {machineId} não foi encontrada.");
            }

            return CompareBomItems(bom1, bom2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao comparar as versões {Version1} e {Version2} da BOM para a máquina ID {MachineId}", version1, version2, machineId);
            throw;
        }
    }

    /// <summary>
    /// Verifica se existem mudanças significativas entre duas listas de itens de BOM.
    /// </summary>
    public bool HasSignificantChanges(List<BomItemDto> oldBom, List<BomItemDto> newBom)
    {
        if (oldBom == null || newBom == null) return true; // Considera mudança significativa se um deles for nulo

        var comparison = CompareBomItems(oldBom, newBom);
        return comparison.HasChanges;
    }

    /// <summary>
    /// Obtém um resumo de todas as versões de BOM para uma determinada máquina.
    /// </summary>
    public async Task<List<BomVersionSummaryDto>> GetMachineVersionsAsync(int machineId)
    {
        return await _context.BomVersions
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

    /// <summary>
    /// Lógica central de comparação de duas listas de itens de BOM.
    /// </summary>
    private BomComparisonResult CompareBomItems(List<BomItemDto> oldBom, List<BomItemDto> newBom)
    {
        var result = new BomComparisonResult();
        var changes = new List<BomDiff>();

        // Usar dicionários para uma busca O(1) melhora a performance em BOMs grandes.
        var oldDict = oldBom.ToDictionary(x => x.PartNumber, x => x);
        var newDict = newBom.ToDictionary(x => x.PartNumber, x => x);

        // Itens removidos (existem na antiga, mas não na nova)
        foreach (var oldItemKVP in oldDict)
        {
            if (!newDict.ContainsKey(oldItemKVP.Key))
            {
                changes.Add(new BomDiff
                {
                    PartNumber = oldItemKVP.Value.PartNumber,
                    Description = oldItemKVP.Value.Description ?? "",
                    Type = "Removed",
                    OldValue = CreateDiffDetail(oldItemKVP.Value)
                });
                result.TotalRemoved++;
            }
        }

        // Itens adicionados e modificados
        foreach (var newItemKVP in newDict)
        {
            if (!oldDict.ContainsKey(newItemKVP.Key))
            {
                // Item Adicionado
                changes.Add(new BomDiff
                {
                    PartNumber = newItemKVP.Value.PartNumber,
                    Description = newItemKVP.Value.Description ?? "",
                    Type = "Added",
                    NewValue = CreateDiffDetail(newItemKVP.Value)
                });
                result.TotalAdded++;
            }
            else
            {
                // Item pode ter sido modificado
                var oldItem = oldDict[newItemKVP.Key];
                if (IsItemModified(oldItem, newItemKVP.Value))
                {
                    changes.Add(new BomDiff
                    {
                        PartNumber = newItemKVP.Value.PartNumber,
                        Description = newItemKVP.Value.Description ?? "",
                        Type = "Modified",
                        OldValue = CreateDiffDetail(oldItem),
                        NewValue = CreateDiffDetail(newItemKVP.Value)
                    });
                    result.TotalModified++;
                }
            }
        }

        result.Changes = changes.OrderBy(x => x.PartNumber).ToList();
        result.HasChanges = changes.Any();

        return result;
    }

    private BomDiffDetail CreateDiffDetail(BomItemDto item)
    {
        return new BomDiffDetail
        {
            Quantity = item.Quantity,
            Description = item.Description,
            StockNumber = item.StockNumber
        };
    }

    private bool IsItemModified(BomItemDto oldItem, BomItemDto newItem)
    {
        return oldItem.Quantity != newItem.Quantity ||
               oldItem.Description != newItem.Description ||
               oldItem.StockNumber != newItem.StockNumber;
    }
}