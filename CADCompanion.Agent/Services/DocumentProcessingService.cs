/ CADCompanion.Agent / Services / DocumentProcessingService.cs
using Microsoft.Extensions.Logging;
using CADCompanion.Agent.Models; // ✅ CORREÇÃO: Garante que o namespace dos modelos seja importado.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CADCompanion.Agent.Services
{
    public interface IDocumentProcessingService
    {
        // O tipo DocumentEventArgs agora será encontrado.
        Task ProcessDocumentEventAsync(DocumentEventArgs args, string eventType);
    }

    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly IInventorConnectionService _inventorConnection;
        private readonly IApiCommunicationService _apiCommunication;
        private readonly IWindowsNotificationService _notificationService;
        private readonly IInventorBOMExtractor _bomExtractor;

        public DocumentProcessingService(
            ILogger<DocumentProcessingService> logger,
            IInventorConnectionService inventorConnection,
            IApiCommunicationService apiCommunication,
            IInventorBOMExtractor bomExtractor,
            IWindowsNotificationService notificationService)
        {
            _logger = logger;
            _inventorConnection = inventorConnection;
            _apiCommunication = apiCommunication;
            _bomExtractor = bomExtractor;
            _notificationService = notificationService;
        }

        public async Task ProcessDocumentEventAsync(DocumentEventArgs args, string eventType)
        {
            try
            {
                _logger.LogInformation("🚀 Evento '{EventType}' recebido para: {FileName}", eventType, args.FileName);

                // Lógica de notificação
                switch (eventType)
                {
                    case "DocumentOpened":
                        _notificationService.ShowDocumentOpenedNotification(args.FileName, args.ProjectName ?? "N/A", 0);
                        break;
                    case "DocumentSaved":
                        _notificationService.ShowSuccessNotification("Arquivo Salvo", $"{args.FileName} foi salvo.");
                        break;
                }

                // Lógica de processamento de BOM para montagens salvas
                if (eventType == "DocumentSaved" && args.DocumentType == DocumentType.Assembly)
                {
                    await ProcessAssemblyDocumentAsync(args);
                }

                // Envia a atividade para a API
                var documentEvent = new DocumentEvent
                {
                    EventType = eventType,
                    FileName = args.FileName,
                    FilePath = args.FilePath,
                    DocumentType = args.DocumentType,
                    Timestamp = DateTime.UtcNow,
                    MachineName = Environment.MachineName,
                    Engineer = Environment.UserName,
                    ProjectId = args.ProjectId,
                    ProjectName = args.ProjectName
                };
                await _apiCommunication.SendDocumentActivityAsync(documentEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar evento de documento. Tipo: {EventType}, Arquivo: {FileName}", eventType, args.FileName);
            }
        }

        private async Task ProcessAssemblyDocumentAsync(DocumentEventArgs documentEvent)
        {
            try
            {
                _logger.LogInformation($"🔧 Extraindo BOM de assembly: {documentEvent.FileName}");

                // Aguarda arquivo estabilizar
                await WaitForFileStabilityAsync(documentEvent.FilePath);

                var bomResult = await _bomExtractor.ExtractBOMAsync(documentEvent.DocumentObject);

                if (bomResult != null && bomResult.Success)
                {
                    _logger.LogInformation($"✅ BOM extraído com {bomResult.BomData.Count} itens.");
                    _notificationService.ShowBOMExtractionNotification(documentEvent.FileName, bomResult.BomData.Count);

                    var bomDataWithContext = new BOMDataWithContext
                    {
                        ProjectId = documentEvent.ProjectId ?? "UNKNOWN",
                        ProjectName = documentEvent.ProjectName ?? "Projeto Desconhecido",
                        AssemblyFileName = documentEvent.FileName,
                        AssemblyFilePath = documentEvent.FilePath,
                        ExtractedAt = DateTime.UtcNow,
                        ExtractedBy = Environment.MachineName,
                        Engineer = Environment.UserName,
                        BOMItems = bomResult.BomData.Select(i => new Models.BOMItem { PartNumber = i.PartNumber, Description = i.Description, Quantity = i.Quantity, Level = i.Level, IsAssembly = i.IsAssembly, Material = i.Material, Weight = i.Weight, Unit = i.Unit, FilePath = i.FilePath }).ToList()
                    };

                    await _apiCommunication.SendBOMDataAsync(bomDataWithContext);
                }
                else
                {
                    _logger.LogWarning($"⚠️ BOM vazio ou erro na extração: {documentEvent.FileName}. Erro: {bomResult?.Error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao processar assembly: {documentEvent.FileName}");
            }
        }

        private async Task WaitForFileStabilityAsync(string filePath)
        {
            const int maxAttempts = 10;
            const int delayMs = 500;
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Arquivo não encontrado para verificação de estabilidade: {FilePath}", filePath);
                return;
            }
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                    _logger.LogDebug("Arquivo estável após {Attempt} tentativas: {FileName}", attempt + 1, Path.GetFileName(filePath));
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(delayMs);
                }
            }
            _logger.LogWarning("Arquivo pode ainda estar em uso após {MaxAttempts} tentativas: {FileName}", maxAttempts, Path.GetFileName(filePath));
        }
    }
}