// CADCompanion.Server/Services/ProjectService.cs
using CADCompanion.Server.Data;
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Services;

public interface IProjectService
{
    Task<List<ProjectSummaryDto>> GetAllProjectsAsync();
    Task<ProjectDto?> GetProjectByIdAsync(int id);
    Task<ProjectDto> CreateProjectAsync(CreateProjectDto createDto);
    Task<ProjectDto?> UpdateProjectAsync(int id, UpdateProjectDto updateDto);
    Task<bool> DeleteProjectAsync(int id);
    Task<List<ProjectSummaryDto>> GetActiveProjectsAsync();
    Task UpdateProjectActivityAsync(int projectId, DateTime activityTime);
    Task UpdateProjectBomCountAsync(int projectId);
}

public class ProjectService : IProjectService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(AppDbContext context, ILogger<ProjectService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ProjectSummaryDto>> GetAllProjectsAsync()
{
    try
    {
        var projects = await _context.Projects
            .OrderByDescending(p => p.LastActivity ?? p.CreatedAt)
            .Select(p => new ProjectSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                ContractNumber = p.ContractNumber,
                Status = p.Status.ToString(),
                Client = p.Client,
                
                // ✅ CAMPOS ADICIONADOS PARA A TABELA
                ResponsibleEngineer = p.ResponsibleEngineer,
                
                ProgressPercentage = p.ProgressPercentage,
                MachineCount = p.MachineCount,
                LastActivity = p.LastActivity,
                CreatedAt = p.CreatedAt,
                EndDate = p.EndDate,
                
                // ✅ CAMPOS FINANCEIROS
                BudgetValue = p.BudgetValue,
                ActualCost = p.ActualCost,
                
                // ✅ CAMPOS DE HORAS
                EstimatedHours = p.EstimatedHours,
                ActualHours = p.ActualHours,
                
                // ✅ CONTADOR DE BOMs
                TotalBomVersions = p.TotalBomVersions
            })
            .ToListAsync();

        _logger.LogInformation("Retornados {Count} projetos com dados expandidos", projects.Count);
        return projects;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao buscar projetos");
        throw;
    }
}

    public async Task<ProjectDto?> GetProjectByIdAsync(int id)
    {
        try
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                _logger.LogWarning("Projeto {ProjectId} não encontrado", id);
                return null;
            }

            return MapToProjectDto(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar projeto {ProjectId}", id);
            throw;
        }
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto createDto)
    {
        try
        {
            // Valida se já existe projeto com mesmo nome
            var existingProject = await _context.Projects
                .FirstOrDefaultAsync(p => p.Name == createDto.Name);

            if (existingProject != null)
            {
                throw new InvalidOperationException($"Já existe um projeto com o nome '{createDto.Name}'");
            }

            var project = new Project
            {
                Name = createDto.Name,
                ContractNumber = createDto.ContractNumber,
                Description = createDto.Description,
                FolderPath = createDto.FolderPath,
                Client = createDto.Client,
                ResponsibleEngineer = createDto.ResponsibleEngineer,
                StartDate = createDto.StartDate,
                EndDate = createDto.EndDate,
                BudgetValue = createDto.BudgetValue,
                EstimatedHours = createDto.EstimatedHours,
                Status = ProjectStatus.Planning,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                MachineCount = createDto.InitialMachines?.Count ?? 0
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Projeto criado: {ProjectName} (ID: {ProjectId})",
                project.Name, project.Id);

            // TODO: Criar pastas do projeto no sistema de arquivos
            if (!string.IsNullOrEmpty(createDto.FolderPath))
            {
                var machineNames = createDto.InitialMachines?.Select(m => m.Name).ToList();
                await CreateProjectFoldersAsync(project.Id, createDto.FolderPath, machineNames);
            }
            if (createDto.InitialMachines != null && createDto.InitialMachines.Count > 0)
            {
                foreach (var machineDto in createDto.InitialMachines)
                {
                    var machine = new Machine
                    {
                        Name = machineDto.Name,
                        OperationNumber = machineDto.OperationNumber,
                        Description = machineDto.Description,
                        FolderPath = machineDto.FolderPath,
                        MainAssemblyPath = machineDto.MainAssemblyPath,
                        ProjectId = project.Id,
                        Status = MachineStatus.Planning,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Machines.Add(machine);
                }

                project.MachineCount = createDto.InitialMachines.Count;
                await _context.SaveChangesAsync();
            }
            return MapToProjectDto(project);
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar projeto: {ProjectName}", createDto.Name);
            throw;
        }

    }

    public async Task<ProjectDto?> UpdateProjectAsync(int id, UpdateProjectDto updateDto)
    {
        try
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                _logger.LogWarning("Projeto {ProjectId} não encontrado para atualização", id);
                return null;
            }

            // Atualiza apenas campos não nulos
            if (updateDto.Name != null) project.Name = updateDto.Name;
            if (updateDto.ContractNumber != null) project.ContractNumber = updateDto.ContractNumber;
            if (updateDto.Description != null) project.Description = updateDto.Description;
            if (updateDto.FolderPath != null) project.FolderPath = updateDto.FolderPath;
            if (updateDto.Client != null) project.Client = updateDto.Client;
            if (updateDto.ResponsibleEngineer != null) project.ResponsibleEngineer = updateDto.ResponsibleEngineer;
            if (updateDto.StartDate.HasValue) project.StartDate = updateDto.StartDate;
            if (updateDto.EndDate.HasValue) project.EndDate = updateDto.EndDate;
            if (updateDto.BudgetValue.HasValue) project.BudgetValue = updateDto.BudgetValue;
            if (updateDto.ActualCost.HasValue) project.ActualCost = updateDto.ActualCost;
            if (updateDto.ProgressPercentage.HasValue) project.ProgressPercentage = updateDto.ProgressPercentage.Value;
            if (updateDto.EstimatedHours.HasValue) project.EstimatedHours = updateDto.EstimatedHours.Value;
            if (updateDto.ActualHours.HasValue) project.ActualHours = updateDto.ActualHours.Value;

            // Atualiza status se fornecido
            if (!string.IsNullOrEmpty(updateDto.Status) &&
                Enum.TryParse<ProjectStatus>(updateDto.Status, out var status))
            {
                project.Status = status;
            }

            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Projeto atualizado: {ProjectName} (ID: {ProjectId})",
                project.Name, project.Id);

            return MapToProjectDto(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar projeto {ProjectId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        try
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                _logger.LogWarning("Projeto {ProjectId} não encontrado para exclusão", id);
                return false;
            }

            // Soft delete - muda status para Cancelled
            project.Status = ProjectStatus.Cancelled;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Projeto excluído (soft delete): {ProjectName} (ID: {ProjectId})",
                project.Name, project.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir projeto {ProjectId}", id);
            throw;
        }
    }

    public async Task<List<ProjectSummaryDto>> GetActiveProjectsAsync()
{
    try
    {
        var activeProjects = await _context.Projects
            .Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Planning)
            .OrderByDescending(p => p.LastActivity ?? p.CreatedAt)
            .Select(p => new ProjectSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                ContractNumber = p.ContractNumber,
                Status = p.Status.ToString(),
                Client = p.Client,
                
                // ✅ CAMPOS ADICIONADOS
                ResponsibleEngineer = p.ResponsibleEngineer,
                
                ProgressPercentage = p.ProgressPercentage,
                MachineCount = p.MachineCount,
                LastActivity = p.LastActivity,
                CreatedAt = p.CreatedAt,
                EndDate = p.EndDate,
                
                // ✅ CAMPOS FINANCEIROS
                BudgetValue = p.BudgetValue,
                ActualCost = p.ActualCost,
                
                // ✅ CAMPOS DE HORAS
                EstimatedHours = p.EstimatedHours,
                ActualHours = p.ActualHours,
                
                // ✅ CONTADOR DE BOMs
                TotalBomVersions = p.TotalBomVersions
            })
            .ToListAsync();

        return activeProjects;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao buscar projetos ativos");
        throw;
    }
}

    public async Task UpdateProjectActivityAsync(int projectId, DateTime activityTime)
    {
        try
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

            if (project != null)
            {
                project.LastActivity = activityTime;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogDebug("Atividade atualizada para projeto {ProjectId}", projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar atividade do projeto {ProjectId}", projectId);
        }
    }

    public async Task UpdateProjectBomCountAsync(int projectId)
    {
        try
        {
            // TODO: Contar BOMs relacionados quando implementar relacionamento
            // Por enquanto, placeholder
            _logger.LogDebug("Contagem de BOMs atualizada para projeto {ProjectId}", projectId);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar contagem de BOMs do projeto {ProjectId}", projectId);
        }
    }

    #region Private Methods

    private static ProjectDto MapToProjectDto(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            ContractNumber = project.ContractNumber,
            Description = project.Description,
            FolderPath = project.FolderPath,
            Status = project.Status.ToString(),
            Client = project.Client,
            ResponsibleEngineer = project.ResponsibleEngineer,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            BudgetValue = project.BudgetValue,
            ActualCost = project.ActualCost,
            ProgressPercentage = project.ProgressPercentage,
            EstimatedHours = project.EstimatedHours,
            ActualHours = project.ActualHours,
            MachineCount = project.MachineCount,
            LastActivity = project.LastActivity,
            TotalBomVersions = project.TotalBomVersions,
            Machines = project.Machines?.Select(m => new MachineSummaryDto
            {
                Id = m.Id,
                Name = m.Name,
                OperationNumber = m.OperationNumber,
                Status = m.Status.ToString(),
                TotalBomVersions = m.TotalBomVersions,
                LastBomExtraction = m.LastBomExtraction
            }).ToList()
        };
    }

    private async Task CreateProjectFoldersAsync(int projectId, string basePath, List<string>? machines)
    {
        try
        {
            // TODO: Implementar criação de pastas do projeto
            // - Pasta raiz do projeto
            // - Subpastas para cada máquina
            // - Estrutura padrão (CAD, Documents, etc.)

            _logger.LogInformation("Criação de pastas do projeto {ProjectId} em {BasePath}",
                projectId, basePath);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar pastas do projeto {ProjectId}", projectId);
        }
    }

    #endregion
}