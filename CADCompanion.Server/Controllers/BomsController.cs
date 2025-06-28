// CADCompanion.Server/Controllers/BomsController.cs - CORRIGIDO
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

    // ✅ CORRIGIDO: Usar o método correto CreateBomVersion
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

            // ✅ USAR O MÉTODO CORRETO que inclui sincronização com catálogo de peças
            var bomVersion = await _versioningService.CreateBomVersion(bomData);

            _logger.LogInformation("✅ BOM V{Version} salva com sucesso para {Path}",
                bomVersion.VersionNumber, bomData.AssemblyFilePath);

            return Ok(new
            {
                saved = true,
                message = $"BOM V{bomVersion.VersionNumber} salva com sucesso",
                versionId = bomVersion.Id,
                versionNumber = bomVersion.VersionNumber,
                itemCount = bomData.Items.Count,
                machineId = bomVersion.MachineId,
                projectId = bomVersion.ProjectId
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("❌ Validação falhou: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar BOM para {Path}", bomData.AssemblyFilePath);
            return StatusCode(500, new
            {
                error = "Erro interno do servidor",
                message = ex.Message
            });
        }
    }

    // ✅ OUTROS MÉTODOS EXISTENTES (não mudam)
    [HttpGet("machine/{machineId}/versions")]
    public async Task<IActionResult> GetMachineVersions(int machineId)
    {
        try
        {
            var versions = await _versioningService.GetMachineVersionsAsync(machineId);
            return Ok(versions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar versões da máquina {MachineId}", machineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("compare/{versionId1}/{versionId2}")]
    public async Task<IActionResult> CompareBomVersions(int versionId1, int versionId2)
    {
        try
        {
            var comparison = await _versioningService.CompareBomVersions(versionId1, versionId2);
            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao comparar versões {V1} e {V2}", versionId1, versionId2);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}