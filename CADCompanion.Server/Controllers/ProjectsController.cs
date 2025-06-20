// CADCompanion.Server/Controllers/ProjectsController.cs - CORRIGIDO COM ENDPOINTS DE MÁQUINAS
using CADCompanion.Server.Services;
using CADCompanion.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CADCompanion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IMachineService _machineService; // ✅ ADICIONADO
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectService projectService,
        IMachineService machineService, // ✅ ADICIONADO
        ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _machineService = machineService; // ✅ ADICIONADO
        _logger = logger;
    }

    #region Endpoints de Projetos Existentes

    [HttpGet]
    public async Task<ActionResult<List<ProjectSummaryDto>>> GetAllProjects()
    {
        try
        {
            _logger.LogInformation("Requisição para listar todos os projetos");
            var projects = await _projectService.GetAllProjectsAsync();
            _logger.LogInformation("Retornando {Count} projetos", projects.Count);
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar projetos");
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<ProjectSummaryDto>>> GetActiveProjects()
    {
        try
        {
            _logger.LogInformation("Requisição para listar projetos ativos");
            var projects = await _projectService.GetActiveProjectsAsync();
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar projetos ativos");
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetProjectById(int id)
    {
        try
        {
            _logger.LogInformation("Requisição para obter projeto {ProjectId}", id);

            var project = await _projectService.GetProjectByIdAsync(id);
            if (project == null)
            {
                _logger.LogWarning("Projeto {ProjectId} não encontrado", id);
                return NotFound($"Projeto {id} não encontrado");
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter projeto {ProjectId}", id);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] CreateProjectDto createProjectDto)
    {
        try
        {
            if (createProjectDto == null)
            {
                return BadRequest("Dados do projeto são obrigatórios");
            }

            _logger.LogInformation("Criando projeto: {ProjectName}", createProjectDto.Name);

            var project = await _projectService.CreateProjectAsync(createProjectDto);

            _logger.LogInformation("Projeto criado com sucesso: {ProjectName} (ID: {ProjectId})",
                project.Name, project.Id);

            return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar projeto: {ProjectName}", createProjectDto?.Name);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProjectDto>> UpdateProject(int id, [FromBody] UpdateProjectDto updateProjectDto)
    {
        try
        {
            if (updateProjectDto == null)
            {
                return BadRequest("Dados de atualização são obrigatórios");
            }

            _logger.LogInformation("Atualizando projeto {ProjectId}", id);

            var project = await _projectService.UpdateProjectAsync(id, updateProjectDto);
            if (project == null)
            {
                return NotFound($"Projeto {id} não encontrado");
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar projeto {ProjectId}", id);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteProject(int id)
    {
        try
        {
            _logger.LogInformation("Excluindo projeto {ProjectId}", id);

            var result = await _projectService.DeleteProjectAsync(id);
            if (!result)
            {
                return NotFound($"Projeto {id} não encontrado");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir projeto {ProjectId}", id);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    #endregion

    #region ✅ ENDPOINTS DE MÁQUINAS ANINHADOS - CORRIGIDOS

    /// <summary>
    /// Lista todas as máquinas de um projeto específico
    /// GET /api/projects/9/machines
    /// </summary>
    [HttpGet("{projectId}/machines")]
    public async Task<ActionResult<IEnumerable<MachineSummaryDto>>> GetMachinesByProject(int projectId)
    {
        try
        {
            _logger.LogInformation("Requisição para listar máquinas do projeto {ProjectId}", projectId);

            var machines = await _machineService.GetMachinesByProjectAsync(projectId);

            _logger.LogInformation("Retornando {Count} máquinas do projeto {ProjectId}",
                machines.Count(), projectId);

            return Ok(machines);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Projeto {ProjectId} não encontrado", projectId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar máquinas do projeto {ProjectId}", projectId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Obter máquina específica de um projeto
    /// GET /api/projects/9/machines/1
    /// </summary>
    [HttpGet("{projectId}/machines/{machineId}")]
    public async Task<ActionResult<MachineDto>> GetMachineByProjectAndId(int projectId, int machineId)
    {
        try
        {
            _logger.LogInformation("Requisição para obter máquina {MachineId} do projeto {ProjectId}",
                machineId, projectId);

            var machine = await _machineService.GetMachineByProjectAndIdAsync(projectId, machineId);
            if (machine == null)
            {
                _logger.LogWarning("Máquina {MachineId} não encontrada no projeto {ProjectId}",
                    machineId, projectId);
                return NotFound($"Máquina {machineId} não encontrada no projeto {projectId}");
            }

            return Ok(machine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter máquina {MachineId} do projeto {ProjectId}",
                machineId, projectId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Criar nova máquina em um projeto
    /// POST /api/projects/9/machines
    /// </summary>
    [HttpPost("{projectId}/machines")]
    public async Task<ActionResult<MachineDto>> CreateMachineInProject(int projectId, [FromBody] CreateMachineDto createMachineDto)
    {
        try
        {
            if (createMachineDto == null)
            {
                return BadRequest("Dados da máquina são obrigatórios");
            }

            _logger.LogInformation("Criando máquina {MachineName} no projeto {ProjectId}",
                createMachineDto.Name, projectId);

            var machine = await _machineService.CreateMachineForProjectAsync(projectId, createMachineDto);

            _logger.LogInformation("Máquina criada com sucesso: {MachineName} (ID: {MachineId}) no projeto {ProjectId}",
                machine.Name, machine.Id, projectId);

            return CreatedAtAction(nameof(GetMachineByProjectAndId),
                new { projectId = projectId, machineId = machine.Id }, machine);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro de validação ao criar máquina no projeto {ProjectId}", projectId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar máquina no projeto {ProjectId}", projectId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Atualizar máquina de um projeto
    /// PUT /api/projects/9/machines/1
    /// </summary>
    [HttpPut("{projectId}/machines/{machineId}")]
    public async Task<ActionResult<MachineDto>> UpdateMachineInProject(
        int projectId,
        int machineId,
        [FromBody] UpdateMachineDto updateMachineDto)
    {
        try
        {
            if (updateMachineDto == null)
            {
                return BadRequest("Dados de atualização são obrigatórios");
            }

            _logger.LogInformation("Atualizando máquina {MachineId} do projeto {ProjectId}",
                machineId, projectId);

            var machine = await _machineService.UpdateMachineInProjectAsync(projectId, machineId, updateMachineDto);
            if (machine == null)
            {
                return NotFound($"Máquina {machineId} não encontrada no projeto {projectId}");
            }

            return Ok(machine);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Máquina {MachineId} não encontrada no projeto {ProjectId}",
                machineId, projectId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar máquina {MachineId} do projeto {ProjectId}",
                machineId, projectId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Excluir máquina de um projeto
    /// DELETE /api/projects/9/machines/1
    /// </summary>
    [HttpDelete("{projectId}/machines/{machineId}")]
    public async Task<ActionResult> DeleteMachineFromProject(int projectId, int machineId)
    {
        try
        {
            _logger.LogInformation("Excluindo máquina {MachineId} do projeto {ProjectId}",
                machineId, projectId);

            var result = await _machineService.DeleteMachineFromProjectAsync(projectId, machineId);
            if (!result)
            {
                return NotFound($"Máquina {machineId} não encontrada no projeto {projectId}");
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Máquina {MachineId} não encontrada no projeto {ProjectId}",
                machineId, projectId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir máquina {MachineId} do projeto {ProjectId}",
                machineId, projectId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    #endregion
}