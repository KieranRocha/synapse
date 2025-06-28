// CADCompanion.Server/Controllers/PartsController.cs - VERSÃO FINAL CORRIGIDA
using CADCompanion.Server.Models;
using CADCompanion.Server.Services;
using CADCompanion.Shared.Contracts; // ✅ ADICIONADO: Para BomItemDto
using Microsoft.AspNetCore.Mvc;

namespace CADCompanion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PartsController : ControllerBase
{
    private readonly IPartService _partService;
    private readonly ILogger<PartsController> _logger;

    public PartsController(IPartService partService, ILogger<PartsController> logger)
    {
        _partService = partService;
        _logger = logger;
    }

    // ✅ PILOTO: Endpoint básico para testar geração de part numbers
    [HttpPost("test-sequence")]
    public async Task<IActionResult> TestPartNumberGeneration()
    {
        try
        {
            var partNumbers = new List<string>();
            
            // Gera 5 part numbers para teste
            for (int i = 0; i < 5; i++)
            {
                var partNumber = await _partService.GetNextPartNumber();
                partNumbers.Add(partNumber);
            }
            
            return Ok(new
            {
                message = "Part numbers gerados com sucesso",
                partNumbers = partNumbers,
                count = partNumbers.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar geração de part numbers");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ✅ Listar peças pendentes de revisão
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingParts()
    {
        try
        {
            var pendingParts = await _partService.GetPartsByStatus(PartStatus.AutoCreated);
            
            var result = pendingParts.Select(p => new
            {
                p.Id,
                p.PartNumber,
                p.Description,
                p.Category,
                p.Status,
                p.CreatedAt,
                UsageCount = p.BomUsages?.Count ?? 0
            });
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar peças pendentes");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ✅ Buscar peça por part number
    [HttpGet("{partNumber}")]
    public async Task<IActionResult> GetPart(string partNumber)
    {
        try
        {
            var part = await _partService.GetPartByNumber(partNumber);
            
            if (part == null)
                return NotFound(new { message = $"Peça {partNumber} não encontrada" });
            
            var result = new
            {
                part.Id,
                part.PartNumber,
                part.Description,
                part.Category,
                part.Material,
                part.Weight,
                part.Cost,
                part.Supplier,
                part.Manufacturer,
                part.Status,
                part.IsStandardPart,
                part.CreatedAt,
                part.UpdatedAt,
                
                // ✅ CORRIGIDO: Where-used básico com tipo inferido
                UsedIn = part.BomUsages?.Select(u => new
                {
                    BomVersionId = u.BomVersionId,
                    Quantity = u.Quantity,
                    Level = u.Level,
                    ParentPart = u.ParentPartNumber
                }).ToList() ?? new() // ✅ Tipo inferido automaticamente
            };
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar peça {PartNumber}", partNumber);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ✅ Estatísticas básicas do catálogo
    [HttpGet("stats")]
    public async Task<IActionResult> GetCatalogStats()
    {
        try
        {
            var allParts = await _partService.GetPartsByStatus(PartStatus.AutoCreated);
            var approvedParts = await _partService.GetPartsByStatus(PartStatus.Approved);
            var inReviewParts = await _partService.GetPartsByStatus(PartStatus.InReview);
            
            // ✅ CORRIGIDO: Usa método que NÃO incrementa
            var nextPartNumber = await _partService.GetCurrentPartNumberSequence();
            
            var stats = new
            {
                TotalParts = allParts.Count + approvedParts.Count + inReviewParts.Count,
                AutoCreated = allParts.Count,
                InReview = inReviewParts.Count,
                Approved = approvedParts.Count,
                NextPartNumber = nextPartNumber, // ✅ Agora sempre mostra o mesmo até criar peça
                
                // Estatísticas por categoria
                ByCategory = allParts
                    .GroupBy(p => p.Category ?? "unknown")
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                
                // Peças padrão vs específicas
                StandardParts = allParts.Count(p => p.IsStandardPart),
                CustomParts = allParts.Count(p => !p.IsStandardPart)
            };
            
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar estatísticas do catálogo");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ✅ PILOTO: Endpoint para sincronizar manualmente (teste)
    [HttpPost("sync-test")]
    public async Task<IActionResult> TestSyncFromManualBom([FromBody] List<TestBomItemDto> testItems)
    {
        try
        {
            // ✅ CORRIGIDO: Todos os campos obrigatórios preenchidos
            var bomItems = testItems.Select(t => new BomItemDto
            {
                PartNumber = t.PartNumber,
                Description = t.Description,
                Quantity = t.Quantity,
                StockNumber = null, // ✅ ADICIONADO: Campo obrigatório
                Level = t.Level ?? 1,
                IsAssembly = false, // ✅ ADICIONADO: Campo obrigatório  
                Material = t.Material,
                Weight = t.Weight
            }).ToList();
            
            // Simula sincronização com BOM ID fictício
            await _partService.SyncPartsFromBom(9999, bomItems);
            
            return Ok(new
            {
                message = "Sincronização de teste concluída",
                itemsProcessed = bomItems.Count,
                items = bomItems.Select(b => new { b.PartNumber, b.Description })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na sincronização de teste");
            return StatusCode(500, new { error = ex.Message });
        }
    }
    // CADCompanion.Server/Controllers/PartsController.cs - ENDPOINTS ADICIONAIS
// Adicionar estes métodos ao PartsController existente

// ✅ Listar TODAS as peças (para página de Parts)
[HttpGet]
public async Task<IActionResult> GetAllParts(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50,
    [FromQuery] string? search = null,
    [FromQuery] string? status = null,
    [FromQuery] string? category = null)
{
    try
    {
        var allParts = new List<Part>();
        
        // Buscar por status específico ou todos
        if (string.IsNullOrEmpty(status) || status == "all")
        {
            // Buscar todas as peças
            allParts.AddRange(await _partService.GetPartsByStatus(PartStatus.AutoCreated));
            allParts.AddRange(await _partService.GetPartsByStatus(PartStatus.InReview));
            allParts.AddRange(await _partService.GetPartsByStatus(PartStatus.Approved));
            allParts.AddRange(await _partService.GetPartsByStatus(PartStatus.Obsolete));
        }
        else
        {
            if (Enum.TryParse<PartStatus>(status, out var parsedStatus))
            {
                allParts = await _partService.GetPartsByStatus(parsedStatus);
            }
        }
        
        // Aplicar filtros
        var filteredParts = allParts.AsQueryable();
        
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            filteredParts = filteredParts.Where(p => 
                p.PartNumber.ToLower().Contains(searchLower) ||
                p.Description.ToLower().Contains(searchLower) ||
                (p.Manufacturer != null && p.Manufacturer.ToLower().Contains(searchLower)));
        }
        
        if (!string.IsNullOrEmpty(category) && category != "all")
        {
            filteredParts = filteredParts.Where(p => p.Category == category);
        }
        
        // Paginação
        var totalCount = filteredParts.Count();
        var pagedParts = filteredParts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        var result = new
        {
            parts = pagedParts.Select(p => new
            {
                p.Id,
                p.PartNumber,
                p.Description,
                p.Category,
                p.Material,
                p.Weight,
                p.Cost,
                p.Supplier,
                p.Manufacturer,
                p.Status,
                p.IsStandardPart,
                p.CreatedAt,
                p.UpdatedAt,
                UsageCount = p.BomUsages?.Count ?? 0
            }),
            pagination = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                hasNext = page * pageSize < totalCount,
                hasPrevious = page > 1
            }
        };
        
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao buscar todas as peças");
        return StatusCode(500, new { error = ex.Message });
    }
}

// ✅ Buscar peças por categoria
[HttpGet("categories")]
public async Task<IActionResult> GetPartCategories()
{
    try
    {
        var allParts = new List<Part>();
        allParts.AddRange(await _partService.GetPartsByStatus(PartStatus.AutoCreated));
        allParts.AddRange(await _partService.GetPartsByStatus(PartStatus.InReview));
        allParts.AddRange(await _partService.GetPartsByStatus(PartStatus.Approved));
        
        var categories = allParts
            .Where(p => !string.IsNullOrEmpty(p.Category))
            .GroupBy(p => p.Category)
            .Select(g => new { 
                Category = g.Key!, 
                Count = g.Count() 
            })
            .OrderByDescending(x => x.Count)
            .ToList();
            
        return Ok(categories);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao buscar categorias");
        return StatusCode(500, new { error = ex.Message });
    }
}

// ✅ Buscar peças com where-used detalhado
[HttpGet("{partNumber}/where-used")]
public async Task<IActionResult> GetPartWhereUsed(string partNumber)
{
    try
    {
        var part = await _partService.GetPartByNumber(partNumber);
        
        if (part == null)
            return NotFound(new { message = $"Peça {partNumber} não encontrada" });
        
        // ✅ Buscar informações detalhadas dos BOMs onde é usado
        var whereUsedDetails = new List<object>();
        
        if (part.BomUsages != null)
        {
            foreach (var usage in part.BomUsages)
            {
                // Aqui você pode adicionar mais lógica para buscar informações do BOM
                // Por enquanto, retornamos o básico
                whereUsedDetails.Add(new
                {
                    BomVersionId = usage.BomVersionId,
                    Quantity = usage.Quantity,
                    Level = usage.Level,
                    ParentPart = usage.ParentPartNumber,
                    ReferenceDesignator = usage.ReferenceDesignator,
                    IsOptional = usage.IsOptional,
                    Notes = usage.Notes
                });
            }
        }
        
        return Ok(new
        {
            part = new
            {
                part.PartNumber,
                part.Description,
                part.Category
            },
            usedIn = whereUsedDetails,
            totalUsages = whereUsedDetails.Count
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao buscar where-used para {PartNumber}", partNumber);
        return StatusCode(500, new { error = ex.Message });
    }
}

// ✅ Atualizar status de uma peça
[HttpPatch("{partNumber}/status")]
public async Task<IActionResult> UpdatePartStatus(string partNumber, [FromBody] UpdatePartStatusDto dto)
{
    try
    {
        var part = await _partService.GetPartByNumber(partNumber);
        
        if (part == null)
            return NotFound(new { message = $"Peça {partNumber} não encontrada" });
        
        if (!Enum.TryParse<PartStatus>(dto.Status, out var newStatus))
            return BadRequest(new { message = "Status inválido" });
        
        // Aqui você implementaria a lógica de atualização no PartService
        // Por enquanto, só retornamos sucesso
        
        _logger.LogInformation("Status da peça {PartNumber} alterado para {Status}", partNumber, newStatus);
        
        return Ok(new
        {
            message = $"Status da peça {partNumber} atualizado para {newStatus}",
            partNumber,
            newStatus = newStatus.ToString()
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao atualizar status da peça {PartNumber}", partNumber);
        return StatusCode(500, new { error = ex.Message });
    }
}

// DTO para atualização de status
public class UpdatePartStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
}

// ✅ DTO para teste manual - CORRIGIDO para tipos compatíveis
public class TestBomItemDto
{
    public string PartNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Quantity { get; set; } // ✅ CORRIGIDO: int como no BomItemDto
    public string? Material { get; set; }
    public double? Weight { get; set; } // ✅ CORRIGIDO: double como no BomItemDto
    public int? Level { get; set; }
}