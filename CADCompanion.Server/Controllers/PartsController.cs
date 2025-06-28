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