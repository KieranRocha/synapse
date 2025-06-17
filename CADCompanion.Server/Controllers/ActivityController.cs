// CADCompanion.Server/Controllers/ActivityController.cs

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ActivityController : ControllerBase
{
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(ILogger<ActivityController> logger)
    {
        _logger = logger;
    }

    [HttpPost("document")]
    public IActionResult LogDocumentActivity([FromBody] object documentEvent)
    {
        _logger.LogInformation("📝 Atividade de documento recebida: {@DocumentEvent}", documentEvent);

        // TODO: Salvar no banco de dados se necessário

        return Ok(new { message = "Atividade registrada com sucesso" });
    }

    [HttpPost("log")]
    public IActionResult LogActivity([FromBody] object activityData)
    {
        _logger.LogInformation("📋 Log de atividade recebido: {@ActivityData}", activityData);

        // TODO: Processar e salvar atividade se necessário

        return Ok(new { message = "Log registrado com sucesso" });
    }
}