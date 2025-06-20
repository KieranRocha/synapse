using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Inventor;
using Microsoft.Extensions.Logging;
using CADCompanion.Shared.Models;
using CADCompanion.Agent.Services;

namespace CADCompanion.Agent.Services
{
    /// <summary>
    /// Interface para processamento de documentos do Inventor
    /// </summary>
    public interface IDocumentProcessingService
    {
        Task<BOMDataWithContext> ExtractBOMAsync(Document document, ProjectInfo project);
        Task ProcessDocumentSaveAsync(Document document, ProjectInfo project);
        bool IsAssemblyDocument(Document document);
        bool IsPartDocument(Document document);
    }

    /// <summary>
    /// Serviço responsável pelo processamento de documentos do Inventor
    /// </summary>
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly IApiCommunicationService _apiService;
        private readonly Application _inventorApp;

        public DocumentProcessingService(
            ILogger<DocumentProcessingService> logger,
            IApiCommunicationService apiService,
            Application inventorApp)
        {
            _logger = logger;
            _apiService = apiService;
            _inventorApp = inventorApp;
        }

        /// <summary>
        /// Verifica se o documento é um assembly
        /// </summary>
        public bool IsAssemblyDocument(Document document)
        {
            return document?.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject;
        }

        /// <summary>
        /// Verifica se o documento é uma peça
        /// </summary>
        public bool IsPartDocument(Document document)
        {
            return document?.DocumentType == DocumentTypeEnum.kPartDocumentObject;
        }

        /// <summary>
        /// Extrai BOM completo de um assembly
        /// </summary>
        public async Task<BOMDataWithContext> ExtractBOMAsync(Document document, ProjectInfo project)
        {
            if (!IsAssemblyDocument(document))
            {
                throw new InvalidOperationException("Documento não é um assembly");
            }

            var assemblyDoc = (AssemblyDocument)document;
            var bomData = new BOMDataWithContext();

            try
            {
                // Preencher dados de contexto
                bomData.FilePath = document.FullFileName;
                bomData.FileName = Path.GetFileName(document.FullFileName);
                bomData.ProjectName = project?.ProjectName ?? "Projeto Desconhecido";
                bomData.ExtractedBy = Environment.UserName;
                bomData.InventorVersion = _inventorApp.SoftwareVersion.DisplayVersion;

                // Obter informações do arquivo
                var fileInfo = new FileInfo(document.FullFileName);
                bomData.FileSizeBytes = fileInfo.Length;

                // Extrair nome da máquina do caminho
                bomData.MachineName = ExtractMachineNameFromPath(document.FullFileName, project);

                // Configurar BOM
                var bom = assemblyDoc.ComponentDefinition.BOM;
                bom.StructuredViewEnabled = true;
                bom.StructuredViewFirstLevelOnly = false;

                var bomView = bom.BOMViews["Estruturado"];
                var bomRows = bomView.BOMRows;

                // Processar linhas do BOM recursivamente
                ProcessBOMRows(bomRows, bomData.BomItems, 0, null);

                // Atualizar totais
                bomData.TotalItems = bomData.BomItems.Count;

                _logger.LogInformation($"BOM extraído com sucesso: {bomData.TotalItems} itens");
                return bomData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao extrair BOM de {document.FullFileName}");
                throw;
            }
        }

        /// <summary>
        /// Processa linhas do BOM recursivamente
        /// </summary>
        private void ProcessBOMRows(BOMRowsEnumerator rows, List<BomItem> items, int level, string parentPartNumber)
        {
            foreach (BOMRow row in rows)
            {
                try
                {
                    var item = new BomItem
                    {
                        Level = level,
                        ParentPartNumber = parentPartNumber,
                        Quantity = (int)row.TotalQuantity
                    };

                    // Obter componente definition
                    var componentDef = row.ComponentDefinitions[1];
                    var document = (Document)componentDef.Document;

                    // Extrair propriedades
                    var designProperties = document.PropertySets["Design Tracking Properties"];
                    var summaryProperties = document.PropertySets["Inventor Summary Information"];
                    var customProperties = document.PropertySets["Inventor User Defined Properties"];

                    // Part Number
                    item.PartNumber = designProperties["Part Number"].Value?.ToString() ??
                                     Path.GetFileNameWithoutExtension(document.FullFileName);

                    // Description
                    item.Description = summaryProperties["Title"].Value?.ToString() ??
                                      designProperties["Description"].Value?.ToString() ?? "";

                    // Material (para peças)
                    if (document.DocumentType == DocumentTypeEnum.kPartDocumentObject)
                    {
                        var partDoc = (PartDocument)document;
                        item.Material = partDoc.ComponentDefinition.Material?.Name ?? "N/A";

                        // Massa
                        var massProps = partDoc.ComponentDefinition.MassProperties;
                        item.Mass = Math.Round(massProps.Mass, 3); // kg
                    }

                    // Categoria
                    item.Category = DetermineCategory(customProperties);

                    // Propriedades customizadas
                    foreach (Property prop in customProperties)
                    {
                        if (prop.Value != null && !string.IsNullOrEmpty(prop.Value.ToString()))
                        {
                            item.CustomProperties[prop.Name] = prop.Value.ToString();
                        }
                    }

                    items.Add(item);

                    // Processar sub-componentes recursivamente
                    if (row.ChildRows != null && row.ChildRows.Count > 0)
                    {
                        ProcessBOMRows(row.ChildRows, items, level + 1, item.PartNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Erro ao processar linha do BOM");
                }
            }
        }

        /// <summary>
        /// Determina a categoria do item baseado em propriedades customizadas
        /// </summary>
        private string DetermineCategory(PropertySet customProperties)
        {
            try
            {
                // Verificar se existe propriedade "Categoria"
                if (customProperties["Categoria"] != null)
                {
                    return customProperties["Categoria"].Value?.ToString() ?? "fabricado";
                }

                // Verificar se existe propriedade "Tipo"
                if (customProperties["Tipo"] != null)
                {
                    var tipo = customProperties["Tipo"].Value?.ToString()?.ToLower();
                    if (tipo?.Contains("comprado") == true) return "comprado";
                    if (tipo?.Contains("normalizado") == true) return "normalizado";
                }
            }
            catch { }

            return "fabricado"; // padrão
        }

        /// <summary>
        /// Extrai nome da máquina do caminho do arquivo
        /// </summary>
        private string ExtractMachineNameFromPath(string filePath, ProjectInfo project)
        {
            try
            {
                // Normalizar caminhos
                var normalizedFilePath = Path.GetFullPath(filePath).ToLower();
                var projectPath = Path.GetFullPath(project.FolderPath).ToLower();

                if (normalizedFilePath.StartsWith(projectPath))
                {
                    var relativePath = normalizedFilePath.Substring(projectPath.Length).TrimStart('\\');
                    var pathParts = relativePath.Split('\\');

                    // Procurar pasta "Maquinas" ou similar
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        if (pathParts[i].Contains("maquina", StringComparison.OrdinalIgnoreCase))
                        {
                            // Próxima pasta deve ser o nome da máquina
                            if (i + 1 < pathParts.Length)
                            {
                                return pathParts[i + 1];
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair nome da máquina do caminho");
            }

            return "Máquina Desconhecida";
        }

        /// <summary>
        /// Processa salvamento de documento
        /// </summary>
        public async Task ProcessDocumentSaveAsync(Document document, ProjectInfo project)
        {
            try
            {
                var activity = new ActivityData
                {
                    Type = "FileSaved",
                    UserName = Environment.UserName,
                    FileName = Path.GetFileName(document.FullFileName),
                    FilePath = document.FullFileName,
                    ProjectName = project?.ProjectName ?? "Desconhecido",
                    MachineName = ExtractMachineNameFromPath(document.FullFileName, project)
                };

                // Adicionar metadados
                activity.Metadata["DocumentType"] = document.DocumentType.ToString();
                activity.Metadata["FileSize"] = new FileInfo(document.FullFileName).Length;

                await _apiService.SendActivityAsync(activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar salvamento de documento");
            }
        }
    }
}