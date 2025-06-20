// CADCompanion.Server/Services/MachineService.cs
public interface IMachineService
{
    Task<List<MachineSummaryDto>> GetMachinesByProjectAsync(int projectId);
    Task<MachineDto?> GetMachineByIdAsync(int id);
    Task<MachineDto> CreateMachineAsync(int projectId, CreateMachineDto createDto);
    Task<MachineDto?> UpdateMachineAsync(int id, UpdateMachineDto updateDto);
    Task<bool> DeleteMachineAsync(int id);
    Task<List<BomVersion>> GetMachineBomVersionsAsync(int machineId);
}

public class MachineService : IMachineService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MachineService> _logger;

    public MachineService(AppDbContext context, ILogger<MachineService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<MachineSummaryDto>> GetMachinesByProjectAsync(int projectId)
    {
        return await _context.Machines
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.OperationNumber)
            .ThenBy(m => m.Name)
            .Select(m => new MachineSummaryDto
            {
                Id = m.Id,
                Name = m.Name,
                OperationNumber = m.OperationNumber,
                Status = m.Status.ToString(),
                TotalBomVersions = m.TotalBomVersions,
                LastBomExtraction = m.LastBomExtraction
            })
            .ToListAsync();
    }

    public async Task<MachineDto?> GetMachineByIdAsync(int id)
    {
        var machine = await _context.Machines
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (machine == null) return null;

        return MapToMachineDto(machine);
    }

    public async Task<MachineDto> CreateMachineAsync(int projectId, CreateMachineDto createDto)
    {
        // Validar se projeto existe
        var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new ArgumentException($"Projeto {projectId} não encontrado");
        }

        var machine = new Machine
        {
            Name = createDto.Name,
            OperationNumber = createDto.OperationNumber,
            Description = createDto.Description,
            FolderPath = createDto.FolderPath,
            MainAssemblyPath = createDto.MainAssemblyPath,
            ProjectId = projectId,
            Status = MachineStatus.Planning,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Machines.Add(machine);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Máquina criada: {MachineName} (ID: {MachineId}) no projeto {ProjectId}",
            machine.Name, machine.Id, projectId);

        return MapToMachineDto(machine);
    }

    private static MachineDto MapToMachineDto(Machine machine)
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
            TotalBomVersions = machine.TotalBomVersions
        };
    }
}