// CADCompanion.Server/Controllers/BomsController.cs - COM DIAGNÓSTICO MELHORADO
using CADCompanion.Server.Services;
using CADCompanion.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class BomsController : ControllerBase
{
    private readonly ILogger<BomsController> _logger;
    private readonly BomVersioningService _versioningService;

    public BomsController(ILogger<BomsController> logger, BomVersioningService versioningService)
    {
        _logger = logger;
        _versioningService = versioningService;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitNewBom([FromBody] BomSubmissionDto bomData)
    {
        _logger.LogInformation("🔄 Recebida nova BOM de {User} para o arquivo: {Path}",
            bomData.ExtractedBy, bomData.AssemblyFilePath);

        try
        {
            // ✅ DIAGNÓSTICO: Log dos dados recebidos
            _logger.LogInformation("📊 Dados recebidos - Projeto: {ProjectId}, Itens: {ItemCount}, Data: {ExtractedAt}",
                bomData.ProjectId ?? "N/A", bomData.Items?.Count ?? 0, bomData.ExtractedAt);

            // ✅ VALIDAÇÃO: Verifica se os dados básicos estão OK
            if (string.IsNullOrEmpty(bomData.AssemblyFilePath))
            {
                _logger.LogWarning("❌ AssemblyFilePath está vazio");
                return BadRequest("AssemblyFilePath é obrigatório");
            }

            if (string.IsNullOrEmpty(bomData.ExtractedBy))
            {
                _logger.LogWarning("❌ ExtractedBy está vazio");
                return BadRequest("ExtractedBy é obrigatório");
            }

            if (bomData.Items == null || bomData.Items.Count == 0)
            {
                _logger.LogWarning("❌ Lista de itens está vazia");
                return BadRequest("Lista de itens não pode estar vazia");
            }

            _logger.LogInformation("✅ Validação básica passou - iniciando processamento no serviço");

            // ✅ PROCESSAMENTO: Chama o serviço com logging detalhado
            var newVersion = await _versioningService.CreateNewVersionAsync(bomData);

            _logger.LogInformation("✅ BOM processada com sucesso - Versão: {Version}, ID: {Id}",
                newVersion.VersionNumber, newVersion.Id);

            return CreatedAtAction(nameof(SubmitNewBom), new { id = newVersion.Id }, newVersion);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "❌ ERRO DE BANCO DE DADOS ao processar BOM");
            _logger.LogError("❌ Inner Exception: {InnerException}", dbEx.InnerException?.Message);
            return StatusCode(500, new
            {
                error = "Erro de banco de dados",
                details = dbEx.InnerException?.Message ?? dbEx.Message
            });
        }
        catch (Npgsql.PostgresException pgEx)
        {
            _logger.LogError(pgEx, "❌ ERRO POSTGRESQL específico ao processar BOM");
            _logger.LogError("❌ PostgreSQL Code: {SqlState}, Message: {Message}", pgEx.SqlState, pgEx.MessageText);
            return StatusCode(500, new
            {
                error = "Erro PostgreSQL",
                code = pgEx.SqlState,
                details = pgEx.MessageText
            });
        }
        catch (System.Text.Json.JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "❌ ERRO DE SERIALIZAÇÃO JSON ao processar BOM");
            return StatusCode(500, new
            {
                error = "Erro de serialização JSON",
                details = jsonEx.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ERRO GERAL ao processar a submissão da BOM");
            _logger.LogError("❌ Exception Type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("❌ Stack Trace: {StackTrace}", ex.StackTrace);

            return StatusCode(500, new
            {
                error = "Erro interno do servidor",
                type = ex.GetType().Name,
                details = ex.Message
            });
        }
    }
}