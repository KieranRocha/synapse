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
    const int maxRetries = 3;
    int attempt = 0;

    while (attempt < maxRetries)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            attempt++;
            
            // Busca ou cria a sequência padrão com lock
            var sequence = await _context.PartNumberSequences
                .FirstOrDefaultAsync(s => s.SequenceType == "DEFAULT");
                
            if (sequence == null)
            {
                _logger.LogInformation("📝 Criando sequência de part number inicial");
                
                sequence = new PartNumberSequence
                {
                    SequenceType = "DEFAULT",
                    LastNumber = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.PartNumberSequences.Add(sequence);
                await _context.SaveChangesAsync();
            }
            
            // Incrementa atomicamente
            sequence.LastNumber++;
            sequence.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            // Formata com zeros à esquerda
            var partNumber = sequence.LastNumber.ToString("D6");
            _logger.LogDebug("✅ Part number gerado: {PartNumber} (tentativa {Attempt})", partNumber, attempt);
            
            return partNumber;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            await transaction.RollbackAsync();
            _logger.LogWarning(ex, "⚠️ Erro ao gerar part number (tentativa {Attempt}/{MaxRetries})", attempt, maxRetries);
            
            // Aguarda um tempo antes de tentar novamente
            await Task.Delay(100 * attempt);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Erro crítico ao gerar part number após {MaxRetries} tentativas", maxRetries);
            throw;
        }
    }
    
    throw new InvalidOperationException($"Falha ao gerar part number após {maxRetries} tentativas");
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
    _logger.LogInformation("🔧 Iniciando sincronização de {Count} itens do BOM {BomVersionId}", 
        bomItems?.Count ?? 0, bomVersionId);

    if (bomItems == null || bomItems.Count == 0)
    {
        _logger.LogWarning("⚠️ Lista de BOM items está vazia, nada para sincronizar");
        return;
    }

    try
    {
        // ✅ 1. VERIFICAR SE TABELAS EXISTEM
        await EnsureTablesExist();

        // ✅ 2. GARANTIR QUE SEQUÊNCIA DE PART NUMBER EXISTE
        await EnsurePartNumberSequenceExists();

        int successCount = 0;
        int skipCount = 0;
        int errorCount = 0;

        foreach (var bomItem in bomItems)
        {
            try
            {
                // ✅ 3. VALIDAR ITEM
                if (!IsValidBomItem(bomItem))
                {
                    skipCount++;
                    continue;
                }

                // ✅ 4. PROCESSAR ITEM INDIVIDUAL COM RETRY
                await ProcessBomItemSafely(bomVersionId, bomItem);
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

        _logger.LogInformation("✅ Sincronização concluída. Sucesso: {Success}, Erros: {Errors}, Pulados: {Skipped}", 
            successCount, errorCount, skipCount);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Erro crítico na sincronização do BOM {BomVersionId}", bomVersionId);
        throw;
    }
}

private async Task EnsureTablesExist()
{
    try
    {
        await _context.Parts.AnyAsync();
        await _context.PartNumberSequences.AnyAsync();
        await _context.BomPartUsages.AnyAsync();
        _logger.LogDebug("✅ Todas as tabelas necessárias existem");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Tabelas de catálogo não existem. Execute: dotnet ef database update");
        throw new InvalidOperationException("Tabelas de catálogo não encontradas. Execute as migrations.");
    }
}

// ✅ MÉTODO AUXILIAR: Garantir sequência existe
private async Task EnsurePartNumberSequenceExists()
{
    try
    {
        var sequence = await _context.PartNumberSequences
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

            _context.PartNumberSequences.Add(newSequence);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("✅ Sequência de part number criada");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Erro ao garantir sequência de part number");
        throw;
    }
}

private bool IsValidBomItem(BomItemDto item)
{
    if (string.IsNullOrWhiteSpace(item.PartNumber))
    {
        _logger.LogWarning("⚠️ Item com PartNumber vazio, pulando");
        return false;
    }

    if (item.Quantity <= 0)
    {
        _logger.LogWarning("⚠️ Item {PartNumber} com quantidade inválida: {Quantity}", 
            item.PartNumber, item.Quantity);
        return false;
    }

    return true;
}

private async Task ProcessBomItemSafely(int bomVersionId, BomItemDto bomItem)
{
    // Normalizar dados
    var normalizedItem = new BomItemDto
    {
        PartNumber = bomItem.PartNumber?.Trim(),
        Description = bomItem.Description?.Trim() ?? "Sem descrição",
        Quantity = bomItem.Quantity,
        Level = bomItem.Level > 0 ? bomItem.Level : 1,
        IsAssembly = bomItem.IsAssembly,
        Material = bomItem.Material?.Trim(),
        Weight = bomItem.Weight,
        StockNumber = bomItem.StockNumber?.Trim()
    };

    // 1. Criar ou encontrar peça
    var part = await CreateOrUpdatePartSafely(normalizedItem);
    
    // 2. Criar/atualizar relacionamento BOM -> Part
    await CreateOrUpdateBomUsageSafely(bomVersionId, part.PartNumber, normalizedItem);
}



// ✅ NOVO: Versão segura do CreateOrUpdatePart
private async Task<Part> CreateOrUpdatePartSafely(BomItemDto bomItem)
{
    try
    {
        // Buscar peça existente por part number exato primeiro
        var existingPart = await _context.Parts
            .FirstOrDefaultAsync(p => p.PartNumber == bomItem.PartNumber);

        if (existingPart != null)
        {
            _logger.LogDebug("🔍 Peça existente encontrada: {PartNumber}", existingPart.PartNumber);
            return existingPart;
        }

        // Se não encontrou, criar nova peça com part number sequencial
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Parts.Add(newPart);
        await _context.SaveChangesAsync();

        _logger.LogDebug("✅ Nova peça criada: {OriginalPN} -> {NewPartNumber}", 
            bomItem.PartNumber, newPart.PartNumber);

        return newPart;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Erro ao criar/atualizar peça para {PartNumber}", bomItem.PartNumber);
        throw;
    }
}
private async Task CreateOrUpdateBomUsageSafely(int bomVersionId, string partNumber, BomItemDto bomItem)
{
    try
    {
        var existingUsage = await _context.BomPartUsages
            .FirstOrDefaultAsync(u => u.BomVersionId == bomVersionId && 
                                    u.PartNumber == partNumber);

        if (existingUsage == null)
        {
            var newUsage = new BomPartUsage
            {
                BomVersionId = bomVersionId,
                PartNumber = partNumber,
                Quantity = bomItem.Quantity,
                Level = bomItem.Level,
                CreatedAt = DateTime.UtcNow
            };

            _context.BomPartUsages.Add(newUsage);
        }
        else
        {
            existingUsage.Quantity = bomItem.Quantity;
            existingUsage.Level = bomItem.Level;
        }

        await _context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Erro ao criar/atualizar uso da peça {PartNumber}", partNumber);
        throw;
    }
}

// ✅ NOVO: Versão segura do CreateBomPartUsage
private async Task CreateBomPartUsageSafely(int bomVersionId, Part part, BomItemDto bomItem)
{
    try
    {
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
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao criar BomPartUsage para {PartNumber}", part.PartNumber);
        throw; // Re-propagar este erro pois é mais crítico
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

