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
        _logger.LogInformation("🔄 Processando nova versão para {FilePath}", dto.AssemblyFilePath);

        try
        {
            // ✅ TESTE DE CONEXÃO: Verifica se o banco está acessível
            _logger.LogInformation("🔗 Testando conexão com banco de dados...");
            var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
            {
                _logger.LogError("❌ Não é possível conectar ao banco de dados");
                throw new InvalidOperationException("Conexão com banco de dados falhou");
            }
            _logger.LogInformation("✅ Conexão com banco OK");

            // ✅ BUSCA ÚLTIMA VERSÃO: Com logging detalhado
            _logger.LogInformation("🔍 Buscando última versão para arquivo: {FilePath}", dto.AssemblyFilePath);

            var lastVersionNumber = await _context.BomVersions
                .Where(v => v.AssemblyFilePath == dto.AssemblyFilePath)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            var newVersionNumber = lastVersionNumber + 1;
            _logger.LogInformation("📊 Última versão encontrada: {LastVersion}, Nova versão será: {NewVersion}",
                lastVersionNumber, newVersionNumber);

            // ✅ VALIDAÇÃO DOS ITENS: Verifica se todos os itens estão válidos
            _logger.LogInformation("🔍 Validando {ItemCount} itens da BOM...", dto.Items.Count);
            for (int i = 0; i < dto.Items.Count; i++)
            {
                var item = dto.Items[i];
                if (string.IsNullOrEmpty(item.PartNumber))
                {
                    _logger.LogWarning("⚠️ Item {Index} tem PartNumber vazio", i);
                }
            }

            // ✅ CRIAÇÃO DA ENTIDADE: Com valores padrão seguros
            _logger.LogInformation("🏗️ Criando nova entidade BomVersion...");

            var newVersion = new BomVersion
            {
                ProjectId = dto.ProjectId ?? "UNKNOWN",
                MachineId = dto.MachineId ?? Environment.MachineName,
                AssemblyFilePath = dto.AssemblyFilePath,
                ExtractedBy = dto.ExtractedBy,
                ExtractedAt = dto.ExtractedAt.ToUniversalTime(), // ✅ CORRIGE TIMEZONE
                VersionNumber = newVersionNumber,
                Items = dto.Items ?? new List<BomItemDto>()
            };

            _logger.LogInformation("📝 Entidade criada - Projeto: {Project}, Máquina: {Machine}, Versão: {Version}",
                newVersion.ProjectId, newVersion.MachineId, newVersion.VersionNumber);

            // ✅ SALVAMENTO: Com logging de cada step
            _logger.LogInformation("💾 Adicionando entidade ao contexto...");
            _context.BomVersions.Add(newVersion);

            _logger.LogInformation("💾 Salvando no banco de dados...");
            var savedRows = await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Salvamento concluído - {SavedRows} linhas afetadas, ID gerado: {Id}",
                savedRows, newVersion.Id);

            _logger.LogInformation("🎉 Sucesso! Versão {VersionNum} salva para {FilePath}",
                newVersion.VersionNumber, newVersion.AssemblyFilePath);

            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ERRO no BomVersioningService.CreateNewVersionAsync");
            _logger.LogError("❌ Tipo da exceção: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("❌ Mensagem: {Message}", ex.Message);

            if (ex.InnerException != null)
            {
                _logger.LogError("❌ Inner Exception: {InnerType} - {InnerMessage}",
                    ex.InnerException.GetType().Name, ex.InnerException.Message);
            }

            throw; // Re-throw para que o controller possa tratar
        }
    }
}