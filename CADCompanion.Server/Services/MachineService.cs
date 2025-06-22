// CADCompanion.Server/Services/MachineService.cs - CORRIGIDO
using CADCompanion.Server.Data;
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Services
{
    public class MachineService : IMachineService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MachineService> _logger;

        public MachineService(AppDbContext context, ILogger<MachineService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Métodos Existentes

        public async Task<IEnumerable<MachineSummaryDto>> GetAllMachinesAsync()
        {
            try
            {
                return await _context.Machines
                    .Select(m => new MachineSummaryDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        OperationNumber = m.OperationNumber,
                        Status = m.Status.ToString(),
                        TotalBomVersions = m.TotalBomVersions,
                        LastBomExtraction = m.LastBomExtraction
                        // StatusColor é calculado automaticamente via property
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar todas as máquinas");
                throw;
            }
        }

        public async Task<MachineDto?> GetMachineByIdAsync(int id)
        {
            try
            {
                var machine = await _context.Machines
                    .Include(m => m.Project)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (machine == null)
                {
                    _logger.LogWarning("Máquina {MachineId} não encontrada", id);
                    return null;
                }

                return ToMachineDto(machine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar máquina {MachineId}", id);
                throw;
            }
        }

        public async Task<MachineDto> CreateMachineAsync(CreateMachineDto createMachineDto)
        {
            try
            {
                // ✅ VALIDAÇÃO: Verificar se projeto existe
                var projectExists = await _context.Projects
                    .AnyAsync(p => p.Id == createMachineDto.ProjectId);

                if (!projectExists)
                {
                    throw new InvalidOperationException($"Projeto {createMachineDto.ProjectId} não encontrado");
                }

                var machine = new Machine
                {
                    Name = createMachineDto.Name,
                    OperationNumber = createMachineDto.OperationNumber,
                    Description = createMachineDto.Description,
                    FolderPath = createMachineDto.FolderPath,
                    MainAssemblyPath = createMachineDto.MainAssemblyPath,
                    ProjectId = createMachineDto.ProjectId,
                    Status = MachineStatus.Planning,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Machines.Add(machine);
                await _context.SaveChangesAsync();

                // ✅ ATUALIZAR CONTADOR NO PROJETO
                await UpdateProjectMachineCountAsync(createMachineDto.ProjectId);

                _logger.LogInformation("Máquina criada: {MachineName} (ID: {MachineId})",
                    machine.Name, machine.Id);

                return ToMachineDto(machine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar máquina: {MachineName}", createMachineDto.Name);
                throw;
            }
        }

        public async Task<MachineDto?> UpdateMachineAsync(int id, UpdateMachineDto updateMachineDto)
        {
            try
            {
                var machine = await _context.Machines.FindAsync(id);
                if (machine == null)
                {
                    _logger.LogWarning("Máquina {MachineId} não encontrada para atualização", id);
                    return null;
                }

                // Atualizar campos
                if (!string.IsNullOrEmpty(updateMachineDto.Name))
                    machine.Name = updateMachineDto.Name;

                if (updateMachineDto.OperationNumber != null)
                    machine.OperationNumber = updateMachineDto.OperationNumber;

                if (updateMachineDto.Description != null)
                    machine.Description = updateMachineDto.Description;

                if (updateMachineDto.FolderPath != null)
                    machine.FolderPath = updateMachineDto.FolderPath;

                if (updateMachineDto.MainAssemblyPath != null)
                    machine.MainAssemblyPath = updateMachineDto.MainAssemblyPath;

                if (updateMachineDto.Status != null && Enum.TryParse<MachineStatus>(updateMachineDto.Status, out var status))
                    machine.Status = status;

                machine.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Máquina atualizada: {MachineName} (ID: {MachineId})", machine.Name, machine.Id);

                return ToMachineDto(machine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar máquina {MachineId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteMachineAsync(int id)
        {
            try
            {
                var machine = await _context.Machines.FindAsync(id);
                if (machine == null)
                {
                    _logger.LogWarning("Máquina {MachineId} não encontrada para exclusão", id);
                    return false;
                }

                var projectId = machine.ProjectId;

                _context.Machines.Remove(machine);
                await _context.SaveChangesAsync();

                await UpdateProjectMachineCountAsync(projectId);

                _logger.LogInformation("Máquina excluída: {MachineName} (ID: {MachineId})", machine.Name, machine.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir máquina {MachineId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<BomVersion>> GetMachineBomVersionsAsync(int machineId)
        {
            try
            {
                return await _context.BomVersions
                    .Where(bv => bv.MachineId == machineId.ToString())
                    .OrderByDescending(bv => bv.VersionNumber)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar versões BOM da máquina {MachineId}", machineId);
                throw;
            }
        }

        #endregion

        #region ✅ NOVOS MÉTODOS PARA PROJETOS

        public async Task<IEnumerable<MachineSummaryDto>> GetMachinesByProjectAsync(int projectId)
        {
            try
            {
                _logger.LogInformation("Buscando máquinas do projeto {ProjectId}", projectId);

                // Verificar se projeto existe
                var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
                if (!projectExists)
                {
                    _logger.LogWarning("Projeto {ProjectId} não encontrado", projectId);
                    throw new InvalidOperationException($"Projeto {projectId} não encontrado");
                }

                var machines = await _context.Machines
                    .Where(m => m.ProjectId == projectId)
                    .Select(m => new MachineSummaryDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        OperationNumber = m.OperationNumber,
                        Status = m.Status.ToString(),
                        TotalBomVersions = m.TotalBomVersions,
                        LastBomExtraction = m.LastBomExtraction
                        // StatusColor é calculado automaticamente
                    })
                    .ToListAsync();

                _logger.LogInformation("Encontradas {Count} máquinas no projeto {ProjectId}",
                    machines.Count, projectId);

                return machines;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar máquinas do projeto {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<MachineDto?> GetMachineByProjectAndIdAsync(int projectId, int machineId)
        {
            try
            {
                var machine = await _context.Machines
                    .Include(m => m.Project)
                    .FirstOrDefaultAsync(m => m.Id == machineId && m.ProjectId == projectId);

                if (machine == null)
                {
                    _logger.LogWarning("Máquina {MachineId} não encontrada no projeto {ProjectId}",
                        machineId, projectId);
                    return null;
                }

                return ToMachineDto(machine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar máquina {MachineId} do projeto {ProjectId}",
                    machineId, projectId);
                throw;
            }
        }

        public async Task<MachineDto> CreateMachineForProjectAsync(int projectId, CreateMachineDto createMachineDto)
        {
            try
            {
                // Verificar se projeto existe
                var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
                if (!projectExists)
                {
                    throw new InvalidOperationException($"Projeto {projectId} não encontrado");
                }

                // Forçar ProjectId correto
                createMachineDto.ProjectId = projectId;

                return await CreateMachineAsync(createMachineDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar máquina no projeto {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<MachineDto?> UpdateMachineInProjectAsync(int projectId, int machineId, UpdateMachineDto updateMachineDto)
        {
            try
            {
                // Verificar se máquina pertence ao projeto
                var machine = await _context.Machines
                    .FirstOrDefaultAsync(m => m.Id == machineId && m.ProjectId == projectId);

                if (machine == null)
                {
                    throw new InvalidOperationException($"Máquina {machineId} não encontrada no projeto {projectId}");
                }

                return await UpdateMachineAsync(machineId, updateMachineDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar máquina {MachineId} do projeto {ProjectId}",
                    machineId, projectId);
                throw;
            }
        }

        public async Task<bool> DeleteMachineFromProjectAsync(int projectId, int machineId)
        {
            try
            {
                // Verificar se máquina pertence ao projeto
                var machine = await _context.Machines
                    .FirstOrDefaultAsync(m => m.Id == machineId && m.ProjectId == projectId);

                if (machine == null)
                {
                    throw new InvalidOperationException($"Máquina {machineId} não encontrada no projeto {projectId}");
                }

                return await DeleteMachineAsync(machineId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir máquina {MachineId} do projeto {ProjectId}",
                    machineId, projectId);
                throw;
            }
        }

        #endregion

        #region Helper Methods

        private static MachineDto ToMachineDto(Machine machine)
        {
            return new MachineDto
            {
                Id = machine.Id,
                Name = machine.Name,
                OperationNumber = machine.OperationNumber,
                Description = machine.Description,
                FolderPath = machine.FolderPath,
                MainAssemblyPath = machine.MainAssemblyPath,
                Status = machine.Status.ToString(),
                ProjectId = machine.ProjectId,
                CreatedAt = machine.CreatedAt,
                UpdatedAt = machine.UpdatedAt,
                LastBomExtraction = machine.LastBomExtraction,
                TotalBomVersions = machine.TotalBomVersions,
                BomVersions = new List<BomVersionSummaryDto>() // ✅ Inicializar vazio
            };
        }
        public async Task<IEnumerable<BomVersion>> GetBomVersionsByAssemblyPathAsync(string assemblyPath)
        {
            var fileName = Path.GetFileName(assemblyPath);
            return await _context.BomVersions
                .Where(bv => bv.AssemblyFilePath.Contains(fileName))
                .OrderByDescending(bv => bv.VersionNumber)
                .ToListAsync();
        }
        private async Task UpdateProjectMachineCountAsync(int projectId)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project != null)
                {
                    var machineCount = await _context.Machines
                        .CountAsync(m => m.ProjectId == projectId);

                    project.MachineCount = machineCount;
                    project.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Contador de máquinas atualizado para projeto {ProjectId}: {Count}",
                        projectId, machineCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar contador de máquinas do projeto {ProjectId}", projectId);
            }
        }
        public async Task<bool> UpdateMachineStatusAsync(int machineId, string status, string? userName, string? currentFile)
        {
            var machine = await _context.Machines.FindAsync(machineId);
            if (machine == null)
            {
                _logger.LogWarning("Máquina com ID {MachineId} não encontrada para atualização de status.", machineId);
                return false;
            }

            // Usamos o Enum para garantir que o status é válido
            if (Enum.TryParse<MachineStatus>(status, true, out var newStatus))
            {
                machine.Status = newStatus;
                machine.UpdatedAt = DateTime.UtcNow;
                // Futuramente, podemos adicionar campos para `WorkingUser` e `CurrentOpenFile` na entidade Machine

                await _context.SaveChangesAsync();
                _logger.LogInformation("Status da máquina {MachineId} atualizado para {Status} pelo usuário {User}", machineId, newStatus, userName ?? "N/A");
                return true;
            }

            _logger.LogWarning("Tentativa de atualizar máquina {MachineId} com status inválido: {Status}", machineId, status);
            return false;
        }
        #endregion
    }

}