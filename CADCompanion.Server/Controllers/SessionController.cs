// Arquivo: CADCompanion.Server/Controllers/SessionController.cs

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly ILogger<SessionController> _logger;

    public SessionController(ILogger<SessionController> logger)
    {
        _logger = logger;
    }

    [HttpPost("heartbeat")]
    public IActionResult Heartbeat()
    {
        _logger.LogInformation("❤️ Heartbeat do Agente recebido com sucesso.");
        return NoContent();
    }
}