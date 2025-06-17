// Em CADCompanion.Server/Services/BomVersioningService.cs
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
        _logger.LogInformation("Processando nova versão para {FilePath}", dto.AssemblyFilePath);

        // 1. Encontra a última versão para este arquivo para incrementar o número
        var lastVersionNumber = await _context.BomVersions
            .Where(v => v.AssemblyFilePath == dto.AssemblyFilePath)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        // 2. Cria a nova entidade BomVersion com os dados do DTO
        var newVersion = new BomVersion
        {
            ProjectId = dto.ProjectId ?? "N/A",
            MachineId = dto.MachineId ?? "N/A",
            AssemblyFilePath = dto.AssemblyFilePath,
            ExtractedBy = dto.ExtractedBy,
            ExtractedAt = dto.ExtractedAt,
            VersionNumber = lastVersionNumber + 1, // Incrementa a versão
            Items = dto.Items // A lista de itens é salva diretamente
        };

        // 3. Adiciona ao DbContext e salva no banco de dados
        _context.BomVersions.Add(newVersion);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Salva com sucesso a versão {VersionNum} para {FilePath}", newVersion.VersionNumber, newVersion.AssemblyFilePath);

        return newVersion;
    }
}