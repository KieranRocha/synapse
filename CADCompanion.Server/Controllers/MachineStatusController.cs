[ApiController]
[Route("api/[controller]")]
public class MachineStatusController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<MachineStatusController> _logger;

    public MachineStatusController(AppDbContext context, ILogger<MachineStatusController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ✅ ENDPOINT que o Agent vai chamar
    [HttpPost("/api/machine-status")]
    public async Task<IActionResult> UpdateMachineStatus([FromBody] MachineStatusRequest request)
    {
        try
        {
            // Desativar status anterior da mesma máquina
            var previousStatus = await _context.MachineStatuses
                .Where(m => m.MachineId == request.MachineId && m.IsActive)
                .ToListAsync();

            foreach (var status in previousStatus)
            {
                status.IsActive = false;
            }

            // Criar novo status
            var newStatus = new MachineStatus
            {
                MachineId = request.MachineId,
                Status = request.Status,
                FileName = request.FileName,
                FilePath = request.FilePath,
                ProjectId = request.ProjectId,
                UserName = request.UserName,
                MachineName = request.MachineName,
                Timestamp = request.Timestamp,
                IsActive = true
            };

            _context.MachineStatuses.Add(newStatus);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Status atualizado: Máquina {request.MachineId} = {request.Status}");

            return Ok(new { success = true, message = "Status atualizado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao atualizar status da máquina {request.MachineId}");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // ✅ ENDPOINT para o frontend
    [HttpGet("active")]
    public async Task<ActionResult<List<MachineStatusResponse>>> GetActiveMachines()
    {
        try
        {
            var activeMachines = await _context.MachineStatuses
                .Where(m => m.IsActive && m.Status != "FECHADA")
                .GroupBy(m => m.MachineId)
                .Select(g => g.OrderByDescending(m => m.Timestamp).First())
                .Select(m => new MachineStatusResponse
                {
                    MachineId = m.MachineId,
                    ProjectId = m.ProjectId,
                    Status = m.Status,
                    FileName = m.FileName,
                    UserName = m.UserName,
                    MachineName = m.MachineName,
                    LastUpdate = m.Timestamp,
                    // BOM vem da última extração
                    CurrentBOM = _context.BOMVersions
                        .Where(b => b.MachineId == m.MachineId)
                        .OrderByDescending(b => b.CreatedAt)
                        .Select(b => b.BOMData)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(activeMachines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar máquinas ativas");
            return StatusCode(500, new { message = ex.Message });
        }
    }
}

// ✅ DTOs
public class MachineStatusRequest
{
    public string MachineId { get; set; }
    public string Status { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string? ProjectId { get; set; }
    public string UserName { get; set; }
    public string MachineName { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MachineStatusResponse
{
    public string MachineId { get; set; }
    public string? ProjectId { get; set; }
    public string Status { get; set; }
    public string FileName { get; set; }
    public string UserName { get; set; }
    public string MachineName { get; set; }
    public DateTime LastUpdate { get; set; }
    public object? CurrentBOM { get; set; } // JSON do BOM
}