using CADCompanion.Server.Data;
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CADCompanion.Server.Services
{
    public class MachineService : IMachineService
    {
        private readonly AppDbContext _context;

        public MachineService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<MachineSummaryDto>> GetAllMachinesAsync()
        {
            return await _context.Machines
                .Select(m => new MachineSummaryDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    MachineCode = m.MachineCode,
                    Status = m.Status.ToString()
                })
                .ToListAsync();
        }

        public async Task<MachineDto> GetMachineByIdAsync(int id)
        {
            var machine = await _context.Machines
                .Include(m => m.BomVersions)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (machine == null)
            {
                return null;
            }

            return ToMachineDto(machine);
        }

        public async Task<MachineDto> CreateMachineAsync(CreateMachineDto createMachineDto)
        {
            var machine = new Machine
            {
                Name = createMachineDto.Name,
                MachineCode = createMachineDto.MachineCode,
                Description = createMachineDto.Description,
                Status = MachineStatus.Active, // Default status
                CreatedAt = DateTime.UtcNow
            };

            _context.Machines.Add(machine);
            await _context.SaveChangesAsync();

            return ToMachineDto(machine);
        }

        public async Task<MachineDto> UpdateMachineAsync(int id, UpdateMachineDto updateMachineDto)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
            {
                return null;
            }

            machine.Name = updateMachineDto.Name;
            machine.MachineCode = updateMachineDto.MachineCode;
            machine.Description = updateMachineDto.Description;
            machine.Status = Enum.Parse<MachineStatus>(updateMachineDto.Status);

            await _context.SaveChangesAsync();
            return ToMachineDto(machine);
        }

        public async Task<bool> DeleteMachineAsync(int id)
        {
            var machine = await _context.Machines.FindAsync(id);
            if (machine == null)
            {
                return false;
            }

            _context.Machines.Remove(machine);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<BomVersion>> GetMachineBomVersionsAsync(int machineId)
        {
            return await _context.BomVersions
               .Where(b => b.MachineId == machineId)
               .ToListAsync();
        }

        private static MachineDto ToMachineDto(Machine machine)
        {
            return new MachineDto
            {
                Id = machine.Id,
                Name = machine.Name,
                MachineCode = machine.MachineCode,
                Description = machine.Description,
                Status = machine.Status.ToString(),
                CreatedAt = machine.CreatedAt,
                BomVersions = machine.BomVersions?.Select(b => new BomVersionDto
                {
                    Id = b.Id,
                    VersionNumber = b.VersionNumber,
                    Description = b.Description,
                    CreatedAt = b.CreatedAt
                }).ToList() ?? new List<BomVersionDto>()
            };
        }
    }
}