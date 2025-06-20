// Services/DocumentProcessingService.cs - CORRIGIDO - LATE BINDING PURO
using Microsoft.Extensions.Logging;
using CADCompanion.Agent.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace CADCompanion.Agent.Services
{
    public interface IDocumentProcessingService
    {
        Task ProcessDocumentSaveAsync(DocumentEvent documentEvent);
        Task ProcessDocumentChangeAsync(DocumentEvent documentEvent);
        Task<BOMDataWithContext?> ExtractBOMWithContextAsync(string filePath, ProjectInfo? projectInfo, string? workSessionId = null);
    }

    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly IInventorConnectionService _inventorConnection;
        private readonly IApiCommunicationService _apiCommunication;
        private readonly InventorBomExtractor _bomExtractor;

        public DocumentProcessingService(
            ILogger<DocumentProcessingService> logger,
            IInventorConnectionService inventorConnection,
            IApiCommunicationService apiCommunication,
            InventorBomExtractor bomExtractor)
        {
            _logger = logger;
            _inventorConnection = inventorConnection;
            _apiCommunication = apiCommunication;
            _bomExtractor = bomExtractor;
        }

        public async Task ProcessDocumentSaveAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogInformation($"📄 Processando save: {documentEvent.FileName} (Tipo: {documentEvent.DocumentType})");

                // Verifica se arquivo ainda existe (pode ter sido movido/deletado)
                if (!File.Exists(documentEvent.FilePath))
                {
                    _logger.LogWarning($"⚠️ Arquivo não encontrado: {documentEvent.FilePath}");
                    return;
                }

                // Aguarda arquivo estabilizar (pode ainda estar sendo escrito)
                await WaitForFileStabilityAsync(documentEvent.FilePath);

                // ✅ NOVA FUNCIONALIDADE: Extrair Machine ID para assemblies
                string? machineId = ExtractMachineIdFromDocument(documentEvent);

                // Processar baseado no tipo do documento
                switch (documentEvent.DocumentType)
                {
                    case DocumentType.Assembly:
                        await ProcessAssemblyDocumentAsync(documentEvent, machineId);
                        break;

                    case DocumentType.Part:
                    case DocumentType.Drawing:
                        await ProcessRegularDocumentAsync(documentEvent);
                        break;

                    default:
                        _logger.LogDebug($"Tipo de documento não monitorado: {documentEvent.DocumentType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar documento: {documentEvent.FilePath}");
            }
        }

        public async Task ProcessDocumentChangeAsync(DocumentEvent documentEvent)
        {
            // Por enquanto, apenas log - implementar conforme necessário
            _logger.LogDebug($"📝 Documento modificado: {documentEvent.FileName}");
            await Task.CompletedTask;
        }

        public async Task<BOMDataWithContext?> ExtractBOMWithContextAsync(string filePath, ProjectInfo? projectInfo, string? workSessionId = null)
        {
            try
            {
                if (!_inventorConnection.IsConnected)
                {
                    throw new InvalidOperationException("Inventor não está conectado");
                }

                _logger.LogInformation($"🔧 Extraindo BOM: {Path.GetFileName(filePath)}");

                var bomItems = _bomExtractor.GetBOMFromFile(filePath);

                var bomData = new BOMDataWithContext
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    ProjectId = projectInfo?.ProjectId,
                    ProjectName = projectInfo?.ProjectName,
                    MachineId = ExtractMachineIdFromFileName(Path.GetFileName(filePath)),
                    WorkSessionId = workSessionId,
                    ExtractedAt = DateTime.UtcNow,
                    ExtractedBy = Environment.UserName,
                    TotalItems = bomItems.Count,
                    BomItems = bomItems
                };

                _logger.LogInformation($"✅ BOM extraído: {bomItems.Count} itens");
                return bomData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao extrair BOM de: {filePath}");
                throw;
            }
        }

        // ===== MÉTODOS PRIVADOS =====

        private async Task ProcessAssemblyDocumentAsync(DocumentEvent documentEvent, string? machineId)
        {
            try
            {
                _logger.LogInformation($"🔧 Processando assembly: {documentEvent.FileName}");

                // Detecta projeto do caminho
                var projectInfo = DetectProjectFromPath(documentEvent.FilePath);

                // Extrai BOM se for assembly principal
                if (IsMainAssemblyFile(documentEvent.FileName, machineId))
                {
                    var bomData = await ExtractBOMWithContextAsync(documentEvent.FilePath, projectInfo);

                    if (bomData != null)
                    {
                        await _apiCommunication.SendBOMDataAsync(bomData);
                        _logger.LogInformation($"📡 BOM enviado para servidor: {bomData.TotalItems} itens");
                    }
                }

                // Registra atividade de salvamento
                _logger.LogInformation($"📄 Atividade registrada: {documentEvent.FileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar assembly: {documentEvent.FilePath}");
            }
        }

        private async Task ProcessRegularDocumentAsync(DocumentEvent documentEvent)
        {
            try
            {
                // Detecta projeto e registra atividade
                var projectInfo = DetectProjectFromPath(documentEvent.FilePath);

                _logger.LogDebug($"📄 Atividade registrada: {documentEvent.FileName}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar documento regular: {documentEvent.FilePath}");
            }
        }

        private async Task SendActivityAsync(DocumentEvent documentEvent, ProjectInfo? projectInfo, string? machineId)
        {
            try
            {
                var activity = new ActivityData
                {
                    Type = "DOCUMENT_SAVE",
                    ProjectId = projectInfo?.ProjectId,
                    ProjectName = projectInfo?.ProjectName,
                    MachineId = machineId,
                    FileName = documentEvent.FileName,
                    FilePath = documentEvent.FilePath,
                    DocumentType = documentEvent.DocumentType.ToString(),
                    Timestamp = DateTime.UtcNow,
                    User = Environment.UserName,
                    CompanionId = Environment.MachineName
                };

                await _apiCommunication.SendActivityAsync(activity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Falha ao enviar atividade para: {documentEvent.FileName}");
            }
        }

        private string? ExtractMachineIdFromDocument(DocumentEvent documentEvent)
        {
            try
            {
                if (documentEvent.DocumentType != DocumentType.Assembly)
                    return null;

                if (!_inventorConnection.IsConnected)
                {
                    _logger.LogWarning("Inventor não conectado para extrair Machine ID");
                    return ExtractMachineIdFromFileName(documentEvent.FileName);
                }

                var inventorApp = _inventorConnection.GetInventorApp();
                if (inventorApp?.Documents == null)
                {
                    return ExtractMachineIdFromFileName(documentEvent.FileName);
                }

                // ✅ CORRIGIDO: Usar dynamic em vez de tipos estáticos
                // Busca o documento aberto na coleção
                dynamic documents = inventorApp.Documents;
                foreach (dynamic doc in documents)
                {
                    if (doc.FullFileName.Equals(documentEvent.FilePath, StringComparison.OrdinalIgnoreCase) &&
                        doc.DocumentType == 12291) // kAssemblyDocumentObject
                    {
                        return ExtractMachineIdFromAssembly(doc);
                    }
                }

                // Fallback: extrair do nome do arquivo
                return ExtractMachineIdFromFileName(documentEvent.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao extrair Machine ID de: {documentEvent.FilePath}");
                return ExtractMachineIdFromFileName(documentEvent.FileName);
            }
        }

        private string? ExtractMachineIdFromAssembly(dynamic assembly)
        {
            try
            {
                // 1. Primeiro tenta iProperties customizadas
                dynamic customProps = assembly.PropertySets["Inventor User Defined Properties"];
                foreach (dynamic prop in customProps)
                {
                    if (prop.Name.Equals("MACHINE_ID", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("ID_MAQUINA", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Equals("MACHINE", StringComparison.OrdinalIgnoreCase))
                    {
                        var machineId = prop.Value?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(machineId))
                        {
                            _logger.LogDebug($"🔧 Machine ID encontrado via iProperty '{prop.Name}': {machineId}");
                            return machineId;
                        }
                    }
                }

                // 2. Fallback: extrair do nome do arquivo
                return ExtractMachineIdFromFileName(assembly.DisplayName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair Machine ID via iProperties");
                return ExtractMachineIdFromFileName(assembly.DisplayName);
            }
        }

        private string? ExtractMachineIdFromFileName(string fileName)
        {
            // Padrões de nomenclatura comuns para máquinas
            var patterns = new[]
            {
                @"^(.+?)_", // Até o primeiro underscore
                @"MAQ_(\w+)", // MAQ_XXXXX
                @"MACHINE_(\w+)", // MACHINE_XXXXX
                @"(\w+)_ASSEMBLY", // XXXXX_ASSEMBLY
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var machineId = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(machineId))
                    {
                        _logger.LogDebug($"🔧 Machine ID extraído do nome: {machineId}");
                        return machineId;
                    }
                }
            }

            return null;
        }

        private ProjectInfo? DetectProjectFromPath(string filePath)
        {
            try
            {
                // Implementar lógica de detecção de projeto baseada no caminho
                // Por enquanto, retorna null - implementar conforme estrutura de pastas
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Erro ao detectar projeto do caminho: {filePath}");
                return null;
            }
        }

        private bool IsMainAssemblyFile(string fileName, string? machineId)
        {
            // Lógica simples: se tem Machine ID e é .iam, considera principal
            return !string.IsNullOrEmpty(machineId) &&
                   fileName.EndsWith(".iam", StringComparison.OrdinalIgnoreCase);
        }

        private async Task WaitForFileStabilityAsync(string filePath)
        {
            const int maxAttempts = 10;
            const int delayMs = 500;

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return; // Arquivo está estável
                }
                catch (IOException)
                {
                    if (i == maxAttempts - 1) throw;
                    await Task.Delay(delayMs);
                }
            }
        }
    }
}