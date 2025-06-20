// CADCompanion.Server/Controllers/ProjectsController.cs
using CADCompanion.Server.Services;
using CADCompanion.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CADCompanion.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos os projetos (resumo)
    /// </summary>
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

    /// <summary>
    /// Lista apenas projetos ativos
    /// </summary>
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

    /// <summary>
    /// Busca projeto por ID (dados completos)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetProjectById(int id)
    {
        try
        {
            _logger.LogInformation("Requisição para buscar projeto {ProjectId}", id);

            var project = await _projectService.GetProjectByIdAsync(id);

            if (project == null)
            {
                _logger.LogWarning("Projeto {ProjectId} não encontrado", id);
                return NotFound($"Projeto com ID {id} não encontrado");
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar projeto {ProjectId}", id);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Cria novo projeto
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] CreateProjectDto createDto)
    {
        try
        {
            _logger.LogInformation("Requisição para criar projeto: {ProjectName}", createDto.Name);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var project = await _projectService.CreateProjectAsync(createDto);

            _logger.LogInformation("Projeto criado com sucesso: {ProjectName} (ID: {ProjectId})",
                project.Name, project.Id);

            return CreatedAtAction(
                nameof(GetProjectById),
                new { id = project.Id },
                project
            );
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro de validação ao criar projeto");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar projeto: {ProjectName}", createDto.Name);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Atualiza projeto existente
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ProjectDto>> UpdateProject(int id, [FromBody] UpdateProjectDto updateDto)
    {
        try
        {
            _logger.LogInformation("Requisição para atualizar projeto {ProjectId}", id);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var project = await _projectService.UpdateProjectAsync(id, updateDto);

            if (project == null)
            {
                _logger.LogWarning("Projeto {ProjectId} não encontrado para atualização", id);
                return NotFound($"Projeto com ID {id} não encontrado");
            }

            _logger.LogInformation("Projeto atualizado com sucesso: {ProjectId}", id);
            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar projeto {ProjectId}", id);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Exclui projeto (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteProject(int id)
    {
        try
        {
            _logger.LogInformation("Requisição para excluir projeto {ProjectId}", id);

            var success = await _projectService.DeleteProjectAsync(id);

            if (!success)
            {
                _logger.LogWarning("Projeto {ProjectId} não encontrado para exclusão", id);
                return NotFound($"Projeto com ID {id} não encontrado");
            }

            _logger.LogInformation("Projeto excluído com sucesso: {ProjectId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir projeto {ProjectId}", id);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Atualiza atividade do projeto (chamado pelo Agent)
    /// </summary>
    [HttpPost("{id}/activity")]
    public async Task<ActionResult> UpdateProjectActivity(int id, [FromBody] UpdateActivityDto activityDto)
    {
        try
        {
            _logger.LogDebug("Atualizando atividade do projeto {ProjectId}", id);

            await _projectService.UpdateProjectActivityAsync(id, activityDto.ActivityTime);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar atividade do projeto {ProjectId}", id);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Busca projetos por status
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<ProjectSummaryDto>>> GetProjectsByStatus(string status)
    {
        try
        {
            _logger.LogInformation("Requisição para buscar projetos com status: {Status}", status);

            var projects = await _projectService.GetAllProjectsAsync();
            var filteredProjects = projects.Where(p =>
                p.Status.Equals(status, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            return Ok(filteredProjects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar projetos por status: {Status}", status);
            return StatusCode(500, "Erro interno do servidor");
        }
    }
}

// DTO auxiliar para atualização de atividade
public class UpdateActivityDto
{
    public DateTime ActivityTime { get; set; } = DateTime.UtcNow;
}