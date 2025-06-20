// Services/DocumentProcessingService.cs - COMPLETO COM MACHINE ID
using Microsoft.Extensions.Logging;
using CADCompanion.Agent.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Inventor;

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
                string? machineId = null;
                if (documentEvent.DocumentType == DocumentType.Assembly)
                {
                    machineId = ExtractMachineIdFromDocument(documentEvent);

                    if (!string.IsNullOrEmpty(machineId))
                    {
                        // Notificar que máquina está sendo trabalhada
                        await NotifyMachineStatus(machineId, "TRABALHANDO", documentEvent);
                        _logger.LogInformation($"🔧 Máquina detectada: {machineId} - Status: TRABALHANDO");
                    }
                }

                // Processa baseado no tipo de documento
                switch (documentEvent.DocumentType)
                {
                    case DocumentType.Assembly:
                        await ProcessAssemblyDocumentAsync(documentEvent, machineId); // ✅ PASSA machineId
                        break;

                    case DocumentType.Part:
                        await ProcessPartDocumentAsync(documentEvent);
                        break;

                    case DocumentType.Drawing:
                        await ProcessDrawingDocumentAsync(documentEvent);
                        break;

                    default:
                        _logger.LogDebug($"Tipo de documento não processado: {documentEvent.DocumentType}");
                        break;
                }

                // Sempre envia atividade para API (auditoria)
                await SendDocumentActivityAsync(documentEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar save do documento: {documentEvent.FileName}");
            }
        }

        public async Task ProcessDocumentChangeAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogDebug($"📝 Processando mudança: {documentEvent.FileName}");

                // Para mudanças simples, só registra atividade
                await SendDocumentActivityAsync(documentEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar mudança do documento: {documentEvent.FileName}");
            }
        }

        #region Machine ID Detection - NOVO

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

                var inventor = _inventorConnection.GetInventor();
                if (inventor?.Documents == null)
                {
                    return ExtractMachineIdFromFileName(documentEvent.FileName);
                }

                // Busca o documento aberto na coleção
                foreach (Document doc in inventor.Documents)
                {
                    if (doc.FullFileName.Equals(documentEvent.FilePath, StringComparison.OrdinalIgnoreCase) &&
                        doc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                    {
                        var assembly = (AssemblyDocument)doc;
                        return ExtractMachineIdFromAssembly(assembly);
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

        private string? ExtractMachineIdFromAssembly(AssemblyDocument assembly)
        {
            try
            {
                // 1. Primeiro tenta iProperties customizadas
                var customProps = assembly.PropertySets["Inventor User Defined Properties"];
                foreach (Property prop in customProps)
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

                // 2. Tenta iProperties de design (caso esteja em outro conjunto)
                try
                {
                    var designProps = assembly.PropertySets["Design Tracking Properties"];
                    foreach (Property prop in designProps)
                    {
                        if (prop.Name.Equals("Part Number", StringComparison.OrdinalIgnoreCase))
                        {
                            var partNumber = prop.Value?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(partNumber))
                            {
                                var machineId = ExtractMachineIdFromPartNumber(partNumber);
                                if (!string.IsNullOrEmpty(machineId))
                                {
                                    _logger.LogDebug($"🔧 Machine ID encontrado via Part Number: {machineId}");
                                    return machineId;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Design Properties não acessíveis: {ex.Message}");
                }

                // 3. Fallback: extrai do nome do arquivo
                var fileName = Path.GetFileNameWithoutExtension(assembly.FullFileName);
                var machineIdFromFile = ExtractMachineIdFromFileName(fileName);

                if (!string.IsNullOrEmpty(machineIdFromFile))
                {
                    _logger.LogDebug($"🔧 Machine ID extraído do nome do arquivo: {machineIdFromFile}");
                    return machineIdFromFile;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair Machine ID do assembly");
                return null;
            }
        }

        private string? ExtractMachineIdFromFileName(string fileName)
        {
            try
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);

                // Padrões comuns para identificar máquinas
                var patterns = new[]
                {
                    @"^(MAQ[-_]\d{2,3})", // MAQ_001, MAQ-001
                    @"^(MAQUINA[-_]\d{2,3})", // MAQUINA_001, MAQUINA-001  
                    @"^(M\d{2,3})", // M001, M01
                    @"^([A-Z]{2,4}[-_]\d{2,4})", // ABC_001, ABCD-1234
                    @"(MAQ[-_]\d{2,3})", // MAQ_001 no meio do nome
                    @"(MAQUINA[-_]\d{2,3})", // MAQUINA_001 no meio
                    @"([A-Z]+[-_]\d{2,3})", // Padrão geral LETRA_NUMERO
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var machineId = match.Groups[1].Value.ToUpper();
                        _logger.LogDebug($"🔧 Machine ID extraído via regex '{pattern}': {machineId}");
                        return machineId;
                    }
                }

                // Padrão específico: se começa com código de projeto + máquina
                // Exemplo: "C2024_001_MAQ_001" -> "MAQ_001"
                var projectMachineMatch = Regex.Match(fileName, @"[A-Z]\d{4}_\d{3}_(.+)", RegexOptions.IgnoreCase);
                if (projectMachineMatch.Success)
                {
                    return projectMachineMatch.Groups[1].Value.ToUpper();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao extrair Machine ID do nome do arquivo: {fileName}");
                return null;
            }
        }

        private string? ExtractMachineIdFromPartNumber(string partNumber)
        {
            // Similar ao ExtractMachineIdFromFileName, mas para Part Numbers
            return ExtractMachineIdFromFileName(partNumber);
        }

        private async Task NotifyMachineStatus(string machineId, string status, DocumentEvent documentEvent)
        {
            try
            {
                var statusData = new MachineStatusData
                {
                    MachineId = machineId,
                    Status = status, // TRABALHANDO, ABERTA, FECHADA
                    FileName = documentEvent.FileName,
                    FilePath = documentEvent.FilePath,
                    ProjectId = documentEvent.ProjectId,
                    ProjectName = documentEvent.ProjectName,
                    UserName = Environment.UserName,
                    MachineName = Environment.MachineName,
                    Timestamp = DateTime.UtcNow,
                    DocumentType = documentEvent.DocumentType.ToString()
                };

                await _apiCommunication.SendMachineStatusAsync(statusData);
                _logger.LogDebug($"🔧 Status enviado: Máquina {machineId} = {status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao enviar status da máquina {machineId}: {ex.Message}");
            }
        }

        #endregion

        #region Assembly Processing

        private async Task ProcessAssemblyDocumentAsync(DocumentEvent documentEvent, string? machineId = null) // ✅ MODIFICADO
        {
            try
            {
                _logger.LogInformation($"🔧 Extraindo BOM de assembly: {documentEvent.FileName}");

                // Extrai BOM com contexto completo
                var bomData = await ExtractBOMWithContextAsync(
                    documentEvent.FilePath,
                    CreateProjectInfoFromEvent(documentEvent),
                    null // workSessionId será obtido via documentEvent se necessário
                );

                if (bomData != null && bomData.BOMItems.Count > 0)
                {
                    // ✅ ADICIONAR machineId ao contexto do BOM
                    bomData.MachineId = machineId;

                    // Envia BOM para API
                    await _apiCommunication.SendBOMDataAsync(bomData);

                    _logger.LogInformation($"✅ BOM enviado: {bomData.TotalItems} itens (Projeto: {bomData.ProjectName}, Máquina: {machineId ?? "N/A"})");
                }
                else
                {
                    _logger.LogWarning($"⚠️ BOM vazio ou erro na extração: {documentEvent.FileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar assembly: {documentEvent.FileName}");
            }
        }

        public async Task<BOMDataWithContext?> ExtractBOMWithContextAsync(string filePath, ProjectInfo? projectInfo, string? workSessionId = null)
        {
            try
            {
                if (!_inventorConnection.IsConnected)
                {
                    _logger.LogError("Inventor não conectado - não é possível extrair BOM");
                    return null;
                }

                // Extrai BOM usando código existente (em background thread)
                var bomItems = await Task.Run(() => _bomExtractor.GetBOMFromFile(filePath));

                if (bomItems == null || bomItems.Count == 0)
                {
                    _logger.LogWarning($"BOM vazio para arquivo: {Path.GetFileName(filePath)}");
                    return null;
                }

                // Cria objeto com contexto rico
                var bomData = new BOMDataWithContext
                {
                    ProjectId = projectInfo?.ProjectId ?? "UNKNOWN",
                    ProjectName = projectInfo?.DetectedName ?? "Projeto Desconhecido",
                    AssemblyFileName = Path.GetFileName(filePath),
                    AssemblyFilePath = filePath,
                    ExtractedAt = DateTime.UtcNow,
                    ExtractedBy = Environment.MachineName,
                    WorkSessionId = workSessionId,
                    Engineer = Environment.UserName,
                    BOMItems = bomItems,
                    InventorVersion = _inventorConnection.InventorVersion ?? "Unknown",
                    MachineId = null // Será preenchido posteriormente
                };

                return bomData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao extrair BOM: {filePath}");
                return null;
            }
        }

        #endregion

        #region Part Processing

        private async Task ProcessPartDocumentAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogDebug($"🔩 Processando part: {documentEvent.FileName}");

                // Para parts, podemos extrair propriedades básicas
                var partProperties = await ExtractPartPropertiesAsync(documentEvent.FilePath);

                if (partProperties != null)
                {
                    await _apiCommunication.SendPartDataAsync(new PartDataWithContext
                    {
                        ProjectId = documentEvent.ProjectId ?? "UNKNOWN",
                        ProjectName = documentEvent.ProjectName ?? "Projeto Desconhecido",
                        PartFileName = documentEvent.FileName,
                        PartFilePath = documentEvent.FilePath,
                        ExtractedAt = DateTime.UtcNow,
                        ExtractedBy = Environment.MachineName,
                        Engineer = documentEvent.Engineer,
                        Properties = partProperties
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar part: {documentEvent.FileName}");
            }
        }

        private async Task<Dictionary<string, object>?> ExtractPartPropertiesAsync(string filePath)
        {
            try
            {
                return await Task.Run(() =>
                {
                    // Aqui poderia usar Inventor API para extrair propriedades da part
                    // Por simplicity, retorna properties básicas
                    var fileInfo = new FileInfo(filePath);

                    return new Dictionary<string, object>
                    {
                        ["FileName"] = fileInfo.Name,
                        ["FileSizeBytes"] = fileInfo.Length,
                        ["LastModified"] = fileInfo.LastWriteTime,
                        ["Extension"] = fileInfo.Extension
                        // TODO: Adicionar properties específicas do Inventor (Material, Mass, Volume, etc.)
                    };
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao extrair propriedades da part: {filePath}");
                return null;
            }
        }

        #endregion

        #region Drawing Processing

        private async Task ProcessDrawingDocumentAsync(DocumentEvent documentEvent)
        {
            try
            {
                _logger.LogDebug($"📐 Processando drawing: {documentEvent.FileName}");

                // Para drawings, registra atividade mas não extrai dados complexos
                // Futuro: poderia extrair lista de views, dimensões, etc.

                await Task.CompletedTask; // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar drawing: {documentEvent.FileName}");
            }
        }

        #endregion

        #region Helper Methods

        private async Task WaitForFileStabilityAsync(string filePath)
        {
            const int maxAttempts = 10;
            const int delayMs = 500;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // Tenta abrir arquivo exclusivo para verificar se está livre
                    using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);

                    // Se chegou aqui, arquivo está livre
                    _logger.LogDebug($"Arquivo estável após {attempt + 1} tentativas: {Path.GetFileName(filePath)}");
                    return;
                }
                catch (IOException)
                {
                    // Arquivo ainda sendo escrito
                    _logger.LogDebug($"Arquivo ainda sendo escrito, tentativa {attempt + 1}: {Path.GetFileName(filePath)}");
                    await Task.Delay(delayMs);
                }
            }

            _logger.LogWarning($"Arquivo pode ainda estar sendo escrito: {Path.GetFileName(filePath)}");
        }

        private ProjectInfo? CreateProjectInfoFromEvent(DocumentEvent documentEvent)
        {
            if (string.IsNullOrEmpty(documentEvent.ProjectId))
                return null;

            return new ProjectInfo
            {
                ProjectId = documentEvent.ProjectId,
                DetectedName = documentEvent.ProjectName ?? "Projeto Desconhecido",
                FolderPath = Path.GetDirectoryName(documentEvent.FilePath) ?? string.Empty,
                IsValidProject = true
            };
        }

        private async Task SendDocumentActivityAsync(DocumentEvent documentEvent)
        {
            try
            {
                // ✅ CORRIGIDO: Enviando DocumentEvent diretamente ao invés de DocumentActivity
                await _apiCommunication.SendDocumentActivityAsync(documentEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar atividade do documento para API");
            }
        }

        #endregion
    }

    #region Supporting Models

    public class PartDataWithContext
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string PartFileName { get; set; } = string.Empty;
        public string PartFilePath { get; set; } = string.Empty;
        public DateTime ExtractedAt { get; set; }
        public string ExtractedBy { get; set; } = string.Empty;
        public string? Engineer { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    // ✅ NOVO MODELO para status da máquina
    public class MachineStatusData
    {
        public string MachineId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string DocumentType { get; set; } = string.Empty;
    }

    #endregion
}