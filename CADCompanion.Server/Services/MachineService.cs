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

        public async Task<IEnumerable<MachineSummaryDto>> GetAllMachinesAsync()
        {
            try
            {
                return await _context.Machines
                    .Select(m => new MachineSummaryDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        OperationNumber = m.OperationNumber, // ✅ CORRIGIDO: era MachineCode
                        Status = m.Status.ToString(),
                        TotalBomVersions = m.TotalBomVersions,
                        LastBomExtraction = m.LastBomExtraction
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
                    OperationNumber = createMachineDto.OperationNumber, // ✅ CORRIGIDO
                    Description = createMachineDto.Description,
                    FolderPath = createMachineDto.FolderPath,
                    MainAssemblyPath = createMachineDto.MainAssemblyPath,
                    ProjectId = createMachineDto.ProjectId,
                    Status = MachineStatus.Planning, // ✅ CORRIGIDO: Status válido
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

                // ✅ ATUALIZAR APENAS CAMPOS NÃO NULOS
                if (updateMachineDto.Name != null) machine.Name = updateMachineDto.Name;
                if (updateMachineDto.OperationNumber != null) machine.OperationNumber = updateMachineDto.OperationNumber;
                if (updateMachineDto.Description != null) machine.Description = updateMachineDto.Description;
                if (updateMachineDto.FolderPath != null) machine.FolderPath = updateMachineDto.FolderPath;
                if (updateMachineDto.MainAssemblyPath != null) machine.MainAssemblyPath = updateMachineDto.MainAssemblyPath;

                // ✅ ATUALIZAR STATUS SE FORNECIDO
                if (!string.IsNullOrEmpty(updateMachineDto.Status) &&
                    Enum.TryParse<MachineStatus>(updateMachineDto.Status, out var status))
                {
                    machine.Status = status;
                }

                machine.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Máquina atualizada: {MachineName} (ID: {MachineId})",
                    machine.Name, machine.Id);

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

                // ✅ ATUALIZAR CONTADOR NO PROJETO
                await UpdateProjectMachineCountAsync(projectId);

                _logger.LogInformation("Máquina excluída: {MachineName} (ID: {MachineId})",
                    machine.Name, machine.Id);

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
                // ✅ BUSCAR BOMs POR MACHINE ID (convertendo para string para compatibilidade)
                return await _context.BomVersions
                   .Where(b => b.MachineId == machineId.ToString())
                   .OrderByDescending(b => b.ExtractedAt)
                   .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar BOMs da máquina {MachineId}", machineId);
                throw;
            }
        }

        #region Private Methods

        private static MachineDto ToMachineDto(Machine machine)
        {
            return new MachineDto
            {
                Id = machine.Id,
                Name = machine.Name,
                OperationNumber = machine.OperationNumber, // ✅ CORRIGIDO
                Description = machine.Description,
                FolderPath = machine.FolderPath,
                MainAssemblyPath = machine.MainAssemblyPath,
                Status = machine.Status.ToString(),
                ProjectId = machine.ProjectId,
                CreatedAt = machine.CreatedAt,
                UpdatedAt = machine.UpdatedAt,
                LastBomExtraction = machine.LastBomExtraction,
                TotalBomVersions = machine.TotalBomVersions,
                BomVersions = new List<BomVersionSummaryDto>() // ✅ INICIALIZAR VAZIO POR ENQUANTO
            };
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

        #endregion
    }
}