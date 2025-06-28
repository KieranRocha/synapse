// CADCompanion.Server/Services/PartService.cs - CORRIGIDO (stats não incrementa)
using CADCompanion.Server.Data;
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Services;

public interface IPartService
{
    Task<string> GetNextPartNumber(); // ✅ INCREMENTA (usar só quando criar peça)
    Task<string> GetCurrentPartNumberSequence(); // ✅ NOVO: SÓ LEITURA (para stats)
    Task<Part?> FindSimilarPart(string description, Dictionary<string, object>? properties = null);
    Task<Part> CreateOrUpdatePart(BomItemDto bomItem);
    Task SyncPartsFromBom(int bomVersionId, List<BomItemDto> bomItems);
    Task<List<Part>> GetPartsByStatus(PartStatus status);
    Task<Part?> GetPartByNumber(string partNumber);
}

public class PartService : IPartService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PartService> _logger;

    public PartService(AppDbContext context, ILogger<PartService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ✅ NOVO: Consulta próximo número SEM incrementar (para estatísticas)
    public async Task<string> GetCurrentPartNumberSequence()
    {
        try
        {
            // Busca sequência atual SEM transação (só leitura)
            var sequence = await _context.PartNumberSequences
                .AsNoTracking() // Otimização: não rastrear mudanças
                .FirstOrDefaultAsync(s => s.SequenceType == "DEFAULT");
                
            if (sequence == null)
            {
                // Se não existe sequência, próximo será 000001
                return "000001";
            }
            
            // Próximo número seria LastNumber + 1
            var nextNumber = sequence.LastNumber + 1;
            var partNumber = nextNumber.ToString("D6");
            
            _logger.LogDebug("Próximo part number (leitura): {PartNumber}", partNumber);
            
            return partNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar sequência de part number");
            throw;
        }
    }

    // ✅ Gera próximo part number sequencial (INCREMENTA - usar só quando criar peça)
    public async Task<string> GetNextPartNumber()
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Busca ou cria a sequência padrão
            var sequence = await _context.PartNumberSequences
                .FirstOrDefaultAsync(s => s.SequenceType == "DEFAULT");
                
            if (sequence == null)
            {
                sequence = new PartNumberSequence
                {
                    SequenceType = "DEFAULT",
                    LastNumber = 0
                };
                _context.PartNumberSequences.Add(sequence);
            }
            
            // ✅ INCREMENTA atomicamente (só aqui!)
            sequence.LastNumber++;
            sequence.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            // Formata com zeros à esquerda: 000001, 000002, etc.
            var partNumber = sequence.LastNumber.ToString("D6");
            _logger.LogInformation("Part number gerado: {PartNumber}", partNumber);
            
            return partNumber;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erro ao gerar part number");
            throw;
        }
    }

    // ✅ Busca peça similar por descrição e propriedades
    public async Task<Part?> FindSimilarPart(string description, Dictionary<string, object>? properties = null)
    {
        try
        {
            // Por enquanto, busca simples por descrição similar
            // Futuro: implementar algoritmo mais sofisticado com propriedades
            var similarParts = await _context.Parts
                .Where(p => EF.Functions.ILike(p.Description, $"%{description}%") ||
                           description.Contains(p.Description))
                .Take(5)
                .ToListAsync();
            
            // Calcula score de similaridade básico
            foreach (var part in similarParts)
            {
                var score = CalculateSimilarityScore(part.Description, description);
                if (score > 0.8) // 80% similar
                {
                    _logger.LogInformation("Peça similar encontrada: {PartNumber} (score: {Score})", 
                        part.PartNumber, score);
                    return part;
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar peça similar para '{Description}'", description);
            return null;
        }
    }

    // ✅ Cria ou atualiza peça baseada em item do BOM
    public async Task<Part> CreateOrUpdatePart(BomItemDto bomItem)
    {
        try
        {
            // Busca peça similar existente
            var existingPart = await FindSimilarPart(bomItem.Description ?? "");
            
            if (existingPart != null)
            {
                // Atualiza dados se necessário
                var updated = false;
                
                if (string.IsNullOrEmpty(existingPart.Material) && !string.IsNullOrEmpty(bomItem.Material))
                {
                    existingPart.Material = bomItem.Material;
                    updated = true;
                }
                
                if (!existingPart.Weight.HasValue && bomItem.Weight.HasValue)
                {
                    existingPart.Weight = (decimal)bomItem.Weight.Value;
                    updated = true;
                }
                
                if (updated)
                {
                    existingPart.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Peça {PartNumber} atualizada", existingPart.PartNumber);
                }
                
                return existingPart;
            }
            else
            {
                // ✅ AQUI usa GetNextPartNumber() que incrementa corretamente
                var newPartNumber = await GetNextPartNumber();
                
                var newPart = new Part
                {
                    PartNumber = newPartNumber,
                    Description = bomItem.Description ?? "Sem descrição",
                    Material = bomItem.Material,
                    Weight = bomItem.Weight.HasValue ? (decimal)bomItem.Weight.Value : null,
                    Category = ClassifyPart(bomItem.Description ?? ""),
                    Status = PartStatus.AutoCreated,
                    IsStandardPart = IsStandardPart(bomItem.Description ?? ""),
                    CustomProperties = ExtractCustomProperties(bomItem),
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Parts.Add(newPart);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Nova peça criada: {PartNumber} - {Description}", 
                    newPart.PartNumber, newPart.Description);
                
                return newPart;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar/atualizar peça para '{Description}'", bomItem.Description);
            throw;
        }
    }

    // ✅ Sincroniza todas as peças de um BOM
    public async Task SyncPartsFromBom(int bomVersionId, List<BomItemDto> bomItems)
    {
        try
        {
            _logger.LogInformation("Sincronizando {Count} itens do BOM {BomVersionId}", 
                bomItems.Count, bomVersionId);
            
            foreach (var bomItem in bomItems)
            {
                // Cria ou atualiza a peça
                var part = await CreateOrUpdatePart(bomItem);
                
                // Cria relacionamento BOM → Part
                var usage = new BomPartUsage
                {
                    BomVersionId = bomVersionId,
                    PartNumber = part.PartNumber,
                    Quantity = bomItem.Quantity,
                    Level = bomItem.Level,
                    ParentPartNumber = null, // Por enquanto null
                    ReferenceDesignator = null, // Por enquanto null
                    CreatedAt = DateTime.UtcNow
                };
                
                // Verifica se já existe para evitar duplicatas
                var existingUsage = await _context.BomPartUsages
                    .FirstOrDefaultAsync(u => u.BomVersionId == bomVersionId && 
                                            u.PartNumber == part.PartNumber);
                                            
                if (existingUsage == null)
                {
                    _context.BomPartUsages.Add(usage);
                }
                else
                {
                    // Atualiza quantidade se mudou
                    existingUsage.Quantity = bomItem.Quantity;
                    existingUsage.Level = bomItem.Level;
                }
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation("Sincronização do BOM {BomVersionId} concluída", bomVersionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao sincronizar peças do BOM {BomVersionId}", bomVersionId);
            throw;
        }
    }

    // ✅ Busca peças por status
    public async Task<List<Part>> GetPartsByStatus(PartStatus status)
    {
        return await _context.Parts
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    // ✅ Busca peça por part number
    public async Task<Part?> GetPartByNumber(string partNumber)
    {
        return await _context.Parts
            .Include(p => p.BomUsages)
            .ThenInclude(u => u.BomVersion)
            .FirstOrDefaultAsync(p => p.PartNumber == partNumber);
    }

    // ✅ Helpers privados (sem mudanças)
    private double CalculateSimilarityScore(string text1, string text2)
    {
        var words1 = text1.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = text2.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var commonWords = words1.Intersect(words2).Count();
        var totalWords = words1.Union(words2).Count();
        
        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }

    private string ClassifyPart(string description)
    {
        var desc = description.ToLower();
        
        if (desc.Contains("rolamento") || desc.Contains("bearing")) return "bearing";
        if (desc.Contains("parafuso") || desc.Contains("screw") || desc.Contains("bolt")) return "fastener";
        if (desc.Contains("motor") || desc.Contains("engine")) return "motor";
        if (desc.Contains("sensor")) return "sensor";
        if (desc.Contains("válvula") || desc.Contains("valve")) return "valve";
        if (desc.Contains("chapa") || desc.Contains("plate")) return "plate";
        if (desc.Contains("tubo") || desc.Contains("pipe")) return "pipe";
        
        return "unknown";
    }

    private bool IsStandardPart(string description)
    {
        var desc = description.ToLower();
        
        return desc.Contains("parafuso") || desc.Contains("screw") ||
               desc.Contains("porca") || desc.Contains("nut") ||
               desc.Contains("arruela") || desc.Contains("washer") ||
               desc.Contains("rolamento") || desc.Contains("bearing");
    }

    private Dictionary<string, object>? ExtractCustomProperties(BomItemDto bomItem)
    {
        var properties = new Dictionary<string, object>();
        
        if (!string.IsNullOrEmpty(bomItem.StockNumber))
            properties["StockNumber"] = bomItem.StockNumber;
            
        if (bomItem.IsAssembly)
            properties["IsAssembly"] = true;
            
        return properties.Any() ? properties : null;
    }
}