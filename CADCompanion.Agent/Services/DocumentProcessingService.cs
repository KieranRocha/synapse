// CADCompanion.Agent/Services/DocumentProcessingService.cs (Corrigido)
using CADCompanion.Agent.Models;
using CADCompanion.Shared.Contracts;
using CADCompanion.Shared.Models;
using Inventor;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CADCompanion.Agent.Services
{
    public class DocumentProcessingService
    {
        private readonly IApiCommunicationService _apiService;
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly InventorBOMExtractor _bomExtractor;

        public DocumentProcessingService(
            IApiCommunicationService apiService,
            ILogger<DocumentProcessingService> logger,
            InventorBOMExtractor bomExtractor)
        {
            _apiService = apiService;
            _logger = logger;
            _bomExtractor = bomExtractor;
        }

        public async Task ProcessDocumentSaveAsync(Document document, ProjectInfo project)
        {
            if (document.DocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Documento de montagem salvo. Iniciando extração de BOM para {FullPath}", document.FullFileName);

                var bomResult = await _bomExtractor.ExtractBOMAsync(document as AssemblyDocument);

                if (bomResult.Success && bomResult.BomItems.Any())
                {
                    var bomSubmission = new BomSubmissionDto
                    {
                        ProjectId = project.Id.ToString(),
                        MachineId = ExtractMachineNameFromPath(document.FullFileName, project),
                        AssemblyFilePath = document.FullFileName,
                        Items = bomResult.BomItems.Select(i => new BomItemDto
                        {
                            PartNumber = i.PartNumber,
                            Quantity = i.Quantity,
                            Description = i.Description,
                            Material = i.Material
                        }).ToList(),
                        ExtractedBy = System.Environment.UserName,
                        ExtractedAt = DateTime.UtcNow
                    };

                    await _apiService.SubmitBomAsync(bomSubmission);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar salvamento do documento.");
            }
        }

        public string ExtractMachineNameFromPath(string filePath, ProjectInfo project)
        {
            if (project?.FolderPath == null) return "N/A";

            var relativePath = filePath.Replace(project.FolderPath, "").TrimStart(System.IO.Path.DirectorySeparatorChar);
            var parts = relativePath.Split(System.IO.Path.DirectorySeparatorChar);

            return parts.Length > 0 ? parts[0] : "N/A";
        }
    }
}