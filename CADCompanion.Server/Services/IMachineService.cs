// CADCompanion.Server/Services/IMachineService.cs - INTERFACE CORRIGIDA
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;

namespace CADCompanion.Server.Services
{
    public interface IMachineService
    {
        // ✅ MÉTODOS ORIGINAIS CORRIGIDOS
        Task<IEnumerable<MachineSummaryDto>> GetAllMachinesAsync();
        Task<MachineDto?> GetMachineByIdAsync(int id);
        Task<MachineDto> CreateMachineAsync(CreateMachineDto createMachineDto);
        Task<MachineDto?> UpdateMachineAsync(int id, UpdateMachineDto updateMachineDto);
        Task<bool> DeleteMachineAsync(int id);
        Task<IEnumerable<BomVersion>> GetMachineBomVersionsAsync(int machineId);

        // ✅ NOVOS MÉTODOS PARA SUPORTE A PROJETOS
        Task<IEnumerable<MachineSummaryDto>> GetMachinesByProjectAsync(int projectId);
        Task<MachineDto?> GetMachineByProjectAndIdAsync(int projectId, int machineId);
        Task<MachineDto> CreateMachineForProjectAsync(int projectId, CreateMachineDto createMachineDto);
        Task<MachineDto?> UpdateMachineInProjectAsync(int projectId, int machineId, UpdateMachineDto updateMachineDto);
        Task<bool> DeleteMachineFromProjectAsync(int projectId, int machineId);
        Task<bool> UpdateMachineStatusAsync(int machineId, string status, string? userName, string? currentFile);
        Task<IEnumerable<BomVersion>> GetBomVersionsByAssemblyPathAsync(string assemblyPath);
    }
}