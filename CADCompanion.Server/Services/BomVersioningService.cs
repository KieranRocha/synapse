// CADCompanion.Server/Services/BomVersioningService.cs - COM DIAGNÓSTICO MELHORADO
using CADCompanion.Server.Data;
using CADCompanion.Server.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Services;

public class BomVersioningService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BomVersioningService> _logger;

    public BomVersioningService(AppDbContext context, ILogger<BomVersioningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BomVersion> CreateNewVersionAsync(BomSubmissionDto dto)
    {
        try
        {
            // Buscar máquina pelo assembly path
            var machine = await _context.Machines
                .FirstOrDefaultAsync(m => m.MainAssemblyPath == dto.AssemblyFilePath);

            if (machine == null)
            {
                var folderPath = Path.GetDirectoryName(dto.AssemblyFilePath);
                machine = await _context.Machines
                    .FirstOrDefaultAsync(m => m.FolderPath != null && folderPath!.Contains(m.FolderPath));
            }

            if (machine == null)
            {
                throw new InvalidOperationException($"Máquina não encontrada para: {dto.AssemblyFilePath}");
            }

            // Próximo número de versão (usando string temporariamente)
            var lastVersion = await _context.BomVersions
                .Where(bv => bv.MachineId == machine.Id.ToString()) // ✅ ToString() temporário
                .OrderByDescending(bv => bv.VersionNumber)
                .FirstOrDefaultAsync();

            var newVersionNumber = (lastVersion?.VersionNumber ?? 0) + 1;

            // Criar versão
            var newVersion = new BomVersion
            {
                ProjectId = machine.ProjectId.ToString(),  // ✅ ToString() temporário
                MachineId = machine.Id.ToString(),         // ✅ ToString() temporário
                AssemblyFilePath = dto.AssemblyFilePath,
                ExtractedBy = dto.ExtractedBy,
                ExtractedAt = dto.ExtractedAt.ToUniversalTime(),
                VersionNumber = newVersionNumber,
                Items = dto.Items ?? new List<BomItemDto>()
            };

            _context.BomVersions.Add(newVersion);
            await _context.SaveChangesAsync();

            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar versão BOM");
            throw;
        }
    }
}