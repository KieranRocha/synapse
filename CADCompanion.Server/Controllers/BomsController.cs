using CADCompanion.Server.Data;
using CADCompanion.Server.Services;
using CADCompanion.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BomsController : ControllerBase
{
    private readonly ILogger<BomsController> _logger;
    private readonly BomVersioningService _versioningService;
    private readonly AppDbContext _context;

    public BomsController(
        ILogger<BomsController> logger,
        BomVersioningService versioningService,
        AppDbContext context)
    {
        _logger = logger;
        _versioningService = versioningService;
        _context = context;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitNewBom([FromBody] BomSubmissionDto bomData)
    {
        _logger.LogInformation("🔄 Recebida nova BOM de {User} para o arquivo: {Path}",
            bomData.ExtractedBy, bomData.AssemblyFilePath);

        try
        {
            // Validação básica
            if (string.IsNullOrEmpty(bomData.AssemblyFilePath))
                return BadRequest("AssemblyFilePath é obrigatório");

            if (string.IsNullOrEmpty(bomData.ExtractedBy))
                return BadRequest("ExtractedBy é obrigatório");

            if (bomData.Items == null || bomData.Items.Count == 0)
                return BadRequest("Lista de itens não pode estar vazia");

            // Verificar se há mudanças antes de salvar
            var lastVersion = await _context.BomVersions
                .Where(bv => bv.AssemblyFilePath == bomData.AssemblyFilePath)
                .OrderByDescending(bv => bv.VersionNumber)
                .FirstOrDefaultAsync();

            if (lastVersion != null)
            {
                var hasChanges = _versioningService.HasSignificantChanges(lastVersion.Items, bomData.Items);
                if (!hasChanges)
                {
                    return Ok(new
                    {
                        saved = false,
                        message = "Nenhuma mudança detectada. BOM não foi salva.",
                        version = lastVersion.VersionNumber
                    });
                }
            }

            // Salvar nova versão
            var newVersion = await _versioningService.CreateNewVersionAsync(bomData);

            _logger.LogInformation("✅ BOM processada com sucesso - Versão: {Version}, ID: {Id}",
                newVersion.VersionNumber, newVersion.Id);

            return Ok(new
            {
                saved = true,
                message = "BOM salva com sucesso",
                id = newVersion.Id,
                version = newVersion.VersionNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar BOM");
            return StatusCode(500, new { error = "Erro interno do servidor", details = ex.Message });
        }
    }

    [HttpGet("machines/{machineId}/versions")]
    public async Task<ActionResult<List<BomVersionSummaryDto>>> GetMachineVersions(int machineId)
    {
        try
        {
            var versions = await _versioningService.GetMachineVersionsAsync(machineId);
            return Ok(versions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar versões da máquina {MachineId}", machineId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpGet("machines/{machineId}/compare/{version1}/{version2}")]
    public async Task<ActionResult<BomComparisonResult>> CompareBomVersions(
        int machineId, int version1, int version2)
    {
        try
        {
            _logger.LogInformation("Comparando versões {V1} e {V2} da máquina {MachineId}",
                version1, version2, machineId);

            var comparison = await _versioningService.CompareBomVersionsAsync(machineId, version1, version2);
            return Ok(comparison);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao comparar versões BOM");
            return StatusCode(500, "Erro interno do servidor");
        }
    }
}