using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CADCompanion.Server.Services
{
    public interface IMachineService
    {
        Task<IEnumerable<MachineSummaryDto>> GetAllMachinesAsync();
        Task<MachineDto> GetMachineByIdAsync(int id);
        Task<MachineDto> CreateMachineAsync(CreateMachineDto createMachineDto);
        Task<MachineDto> UpdateMachineAsync(int id, UpdateMachineDto updateMachineDto);
        Task<bool> DeleteMachineAsync(int id);
        Task<IEnumerable<BomVersion>> GetMachineBomVersionsAsync(int machineId);
    }
}