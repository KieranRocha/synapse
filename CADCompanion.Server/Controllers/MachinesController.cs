// CADCompanion.Server/Controllers/MachinesController.cs
[ApiController]
[Route("api/projects/{projectId:int}/[controller]")]
public class MachinesController : ControllerBase
{
    private readonly IMachineService _machineService;
    private readonly ILogger<MachinesController> _logger;

    public MachinesController(IMachineService machineService, ILogger<MachinesController> logger)
    {
        _machineService = machineService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<MachineSummaryDto>>> GetMachines(int projectId)
    {
        try
        {
            var machines = await _machineService.GetMachinesByProjectAsync(projectId);
            return Ok(machines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar máquinas do projeto {ProjectId}", projectId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MachineDto>> GetMachine(int projectId, int id)
    {
        try
        {
            var machine = await _machineService.GetMachineByIdAsync(id);

            if (machine == null || machine.ProjectId != projectId)
            {
                return NotFound();
            }

            return Ok(machine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar máquina {MachineId}", id);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpPost]
    public async Task<ActionResult<MachineDto>> CreateMachine(int projectId, [FromBody] CreateMachineDto createDto)
    {
        try
        {
            var machine = await _machineService.CreateMachineAsync(projectId, createDto);

            return CreatedAtAction(
                nameof(GetMachine),
                new { projectId = projectId, id = machine.Id },
                machine
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar máquina no projeto {ProjectId}", projectId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }
}