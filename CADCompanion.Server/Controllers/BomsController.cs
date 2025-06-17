// Em CADCompanion.Server/Controllers/BomsController.cs
using CADCompanion.Server.Services; // Adicione este using
using CADCompanion.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class BomsController : ControllerBase
{
    private readonly ILogger<BomsController> _logger;
    private readonly BomVersioningService _versioningService; // Injetar o serviço

    // Modifique o construtor
    public BomsController(ILogger<BomsController> logger, BomVersioningService versioningService)
    {
        _logger = logger;
        _versioningService = versioningService;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitNewBom([FromBody] BomSubmissionDto bomData)
    {
        _logger.LogInformation(
            "Recebida nova BOM de {User} para o arquivo: {Path}",
            bomData.ExtractedBy, bomData.AssemblyFilePath);

        try
        {
            // Substitua o código antigo por esta chamada
            var newVersion = await _versioningService.CreateNewVersionAsync(bomData);
            // Retorna um status 201 Created com a localização do novo recurso
            return CreatedAtAction(nameof(SubmitNewBom), new { id = newVersion.Id }, newVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar a submissão da BOM.");
            return StatusCode(500, "Ocorreu um erro interno no servidor.");
        }
    }
}