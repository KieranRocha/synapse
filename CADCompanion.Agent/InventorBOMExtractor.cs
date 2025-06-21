using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CADCompanion.Agent.Models;
using CADCompanion.Agent.Services;
using OfficeOpenXml; // Certifique-se de que o pacote NuGet 'EPPlus' está instalado

namespace CADCompanion.Agent
{
    // Classe para representar uma montagem disponível (mantida para compatibilidade)
    public class AssemblyInfo
    {
        public string DisplayName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsActive { get; set; } = false;
        public dynamic Document { get; set; } = null!;
    }

    // ===== NOVAS CLASSES PARA WEB API =====
    public class OpenAssemblyInfo
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public bool IsActive { get; set; }
        public bool IsSaved { get; set; }
        public string DocumentType { get; set; } = "";
    }

    public class ActiveDocumentInfo
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string DocumentType { get; set; } = "";
        public bool IsSaved { get; set; }
        public bool IsAssembly { get; set; }
        public DateTime? LastSaved { get; set; }
    }

    // Classe COM Helper (mesma de antes)
    internal static class ComHelper
    {
        [DllImport("ole32.dll")]
        private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);

        [DllImport("oleaut32.dll")]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        public static object? GetActiveObject(string progId)
        {
            try
            {
                var result = CLSIDFromProgID(progId, out Guid clsid);
                if (result != 0) return null;

                result = GetActiveObject(ref clsid, IntPtr.Zero, out object obj);
                if (result != 0) return null;

                return obj;
            }
            catch
            {
                return null;
            }
        }
    }

    // ✅ CORRIGIDO: A classe agora implementa a interface IInventorBOMExtractor
    public class InventorBomExtractor : IInventorBOMExtractor
    {
        private dynamic? _inventorApp;

        static InventorBomExtractor()
        {
            // Para uso não comercial sob a licença Polyform, o EPPlus 8+ não requer
            // que nenhuma propriedade de licença seja definida.
        }

        /// <summary>
        /// ✅ NOVO: Implementação do método da interface IInventorBOMExtractor.
        /// Este método é assíncrono e retorna o tipo BOMExtractionResult.
        /// </summary>
        public async Task<BOMExtractionResult> ExtractBOMAsync(dynamic assemblyDoc)
        {
            var result = new BOMExtractionResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Reutiliza o método síncrono existente dentro de uma Task para não bloquear a thread principal.
                var internalBomItemsDynamic = await Task.Run(() => GetBOMFromDocument(assemblyDoc));
                var internalBomItems = (internalBomItemsDynamic as IEnumerable<BomItem>)?.ToList() ?? new List<BomItem>();

                // Mapeia da classe 'BomItem' local para a classe 'BOMItem' do namespace Models.
                result.BomData = internalBomItems.Select(item => new Models.BOMItem
                {
                    // O Id será gerado automaticamente pelo construtor do modelo.
                    PartNumber = item.PartNumber,
                    Description = item.Description,
                    Quantity = Convert.ToInt32(item.Quantity) // Garante que a quantidade seja um inteiro.
                    // Outras propriedades podem ser mapeadas aqui se necessário.
                }).ToList();

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            finally
            {
                try
                {
                    Marshal.ReleaseComObject(assemblyDoc);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch { }
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed.TotalSeconds;
            }
            return result;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void ConnectToInventor()
        {
            try
            {
                Console.WriteLine("Tentando conectar à instância ativa...");
                var activeApp = ComHelper.GetActiveObject("Inventor.Application");

                if (activeApp != null)
                {
                    _inventorApp = activeApp;
                    string versao = _inventorApp.SoftwareVersion.DisplayVersion;
                    Console.WriteLine($"✓ Conectado à instância ativa - Inventor {versao}");
                    if (!_inventorApp.Visible)
                    {
                        Console.WriteLine("  Instância ativa do Inventor não visível. Tentando torná-la visível...");
                        _inventorApp.Visible = true;
                        System.Threading.Thread.Sleep(2000);
                        Console.WriteLine("  Instância agora visível.");
                    }
                    else
                    {
                        Console.WriteLine("  Instância ativa do Inventor já está visível.");
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Falha ao conectar à instância ativa (erro: {ex.Message}).");
            }

            try
            {
                Console.WriteLine("Nenhuma instância ativa adequada encontrada ou falha ao conectar. Criando nova instância...");
                Type? inventorType = Type.GetTypeFromProgID("Inventor.Application");
                if (inventorType == null) throw new InvalidOperationException("Inventor não está instalado ou ProgID inválido.");
                _inventorApp = Activator.CreateInstance(inventorType);
                if (_inventorApp == null) throw new InvalidOperationException("Não foi possível criar uma nova instância do Inventor.");
                _inventorApp.Visible = true;
                System.Threading.Thread.Sleep(5000);
                string versao = _inventorApp.SoftwareVersion.DisplayVersion;
                Console.WriteLine($"✓ Nova instância criada - Inventor {versao}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Falha total ao conectar ou criar instância do Inventor: {ex.Message}");
            }
            if (_inventorApp != null)
            {
                Console.WriteLine($"✓ Inventor conectado - Versão: {GetInventorVersion()}");
            }
        }

        public List<AssemblyInfo> ListAvailableAssemblies()
        {
            var assemblies = new List<AssemblyInfo>();
            if (_inventorApp == null) return assemblies;

            try
            {
                dynamic documents = _inventorApp.Documents;
                int count = documents.Count;
                Console.WriteLine($"📄 Total de documentos abertos na instância conectada: {count}");

                dynamic? activeDoc = null;
                try { activeDoc = _inventorApp.ActiveDocument; }
                catch { Console.WriteLine("⚠️ Nenhum documento ativo detectado na instância conectada."); }

                if (count > 0)
                {
                    for (int i = 1; i <= count; i++)
                    {
                        try
                        {
                            dynamic doc = documents.Item[i];
                            if (doc.DocumentType == 12291) // kAssemblyDocumentObject
                            {
                                string displayName = doc.DisplayName?.ToString() ?? "Nome não disponível";
                                string fullPath = doc.FullFileName?.ToString() ?? "Caminho não disponível";
                                bool isActive = activeDoc != null && (activeDoc.FullFileName?.ToString() ?? "").Equals(fullPath, StringComparison.OrdinalIgnoreCase);

                                assemblies.Add(new AssemblyInfo { DisplayName = displayName, FullPath = fullPath, IsActive = isActive, Document = doc });
                                Console.WriteLine($"  ✓ Montagem encontrada: {displayName} {(isActive ? "[ATIVA]" : "")}");
                            }
                            else
                            {
                                Console.WriteLine($"  🔍 Documento encontrado, mas não é montagem (Tipo: {doc.DocumentType}): {doc.DisplayName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ⚠️ Erro ao processar documento {i}: {ex.Message}");
                        }
                    }
                }
                Console.WriteLine($"📦 Total de montagens encontradas na instância conectada: {assemblies.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao listar documentos: {ex.Message}");
            }
            return assemblies;
        }

        public List<BomItem> GetBOMFromDocument(dynamic assemblyDoc)
        {
            try
            {
                Console.WriteLine($"Processando documento: {assemblyDoc.DisplayName}");
                return ExtractBOM(assemblyDoc);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao processar documento: {ex.Message}", ex);
            }
        }

        public List<BomItem> GetBOMFromFile(string assemblyFilePath)
        {
            if (_inventorApp == null) throw new InvalidOperationException("Não conectado ao Inventor.");

            dynamic? doc = null;
            try
            {
                Console.WriteLine($"Abrindo arquivo: {assemblyFilePath}");
                doc = _inventorApp.Documents.Open(assemblyFilePath, false);
                int docType = doc.DocumentType;
                Console.WriteLine($"Tipo do arquivo aberto: {docType}");

                if (docType != 12291)
                {
                    try
                    {
                        dynamic compDef = doc.ComponentDefinition;
                        dynamic bom = compDef.BOM;
                        Console.WriteLine("✓ Detectado como montagem/sub-montagem via BOM (pode ser uma peça com BOM)");
                    }
                    catch
                    {
                        throw new InvalidOperationException($"Arquivo '{Path.GetFileName(assemblyFilePath)}' não é uma montagem válida para extração de BOM. Tipo detectado: {docType}.");
                    }
                }
                else
                {
                    Console.WriteLine("✓ Confirmado como montagem (.iam)");
                }

                return ExtractBOM(doc);
            }
            finally
            {
                try
                {
                    if (doc != null)
                    {
                        doc.Close(false);
                        Marshal.ReleaseComObject(doc);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro ao fechar o documento: {ex.Message}");
                }
            }
        }

        public List<OpenAssemblyInfo> ListOpenAssemblies()
        {
            var assemblies = new List<OpenAssemblyInfo>();
            try
            {
                if (_inventorApp?.Documents == null)
                {
                    Console.WriteLine("❌ Inventor não conectado ou sem documentos");
                    return assemblies;
                }

                Console.WriteLine($"🔍 Verificando {_inventorApp.Documents.Count} documentos abertos...");
                for (int i = 1; i <= _inventorApp.Documents.Count; i++)
                {
                    try
                    {
                        dynamic doc = _inventorApp.Documents[i];
                        if (doc.DocumentType == 12291) // kAssemblyDocumentObject
                        {
                            var assemblyInfo = new OpenAssemblyInfo
                            {
                                FileName = doc.DisplayName,
                                FilePath = doc.FullFileName ?? doc.DisplayName,
                                IsActive = doc == _inventorApp.ActiveDocument,
                                IsSaved = !doc.Dirty,
                                DocumentType = "Assembly (.iam)"
                            };
                            assemblies.Add(assemblyInfo);
                            Console.WriteLine($"  ✅ Assembly encontrado: {assemblyInfo.FileName} {(assemblyInfo.IsActive ? "[ATIVO]" : "")}");
                        }
                        else
                        {
                            Console.WriteLine($"  🔍 Documento encontrado, mas não é assembly (Tipo: {doc.DocumentType}): {doc.DisplayName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ Erro ao processar documento {i}: {ex.Message}");
                    }
                }
                Console.WriteLine($"📦 Total de assemblies encontrados: {assemblies.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao listar assemblies abertos: {ex.Message}");
            }
            return assemblies;
        }

        public List<BomItem> GetBOMFromOpenAssembly(string fileName)
        {
            try
            {
                if (_inventorApp?.Documents == null) throw new InvalidOperationException("Inventor não conectado.");

                Console.WriteLine($"🔍 Procurando assembly aberto: {fileName}");
                for (int i = 1; i <= _inventorApp.Documents.Count; i++)
                {
                    try
                    {
                        dynamic doc = _inventorApp.Documents[i];
                        if (doc.DisplayName.Equals(fileName, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(doc.FullFileName ?? "").Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (doc.DocumentType == 12291)
                            {
                                Console.WriteLine($"✅ Assembly encontrado e ativo: {doc.DisplayName}");
                                return GetBOMFromDocument(doc);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Documento '{fileName}' não é um assembly (tipo: {doc.DocumentType})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Erro ao verificar documento {i}: {ex.Message}");
                    }
                }
                throw new FileNotFoundException($"Assembly '{fileName}' não encontrado nos documentos abertos.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao extrair BOM de assembly aberto: {ex.Message}");
                throw;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public bool OpenDocument(string filePath)
        {
            try
            {
                if (_inventorApp == null) throw new InvalidOperationException("Inventor não conectado.");
                if (!File.Exists(filePath)) throw new FileNotFoundException($"Arquivo não encontrado: {filePath}");

                Console.WriteLine($"📂 Abrindo documento: {filePath}");
                for (int i = 1; i <= _inventorApp.Documents.Count; i++)
                {
                    try
                    {
                        dynamic doc = _inventorApp.Documents[i];
                        if (doc.FullFileName?.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            Console.WriteLine($"✅ Documento já está aberto: {doc.DisplayName}");
                            _inventorApp.ActiveDocument = doc;
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Erro ao verificar documento aberto {i}: {ex.Message}");
                    }
                }

                dynamic openedDoc = _inventorApp.Documents.Open(filePath, true);
                Console.WriteLine($"✅ Documento aberto com sucesso: {openedDoc.DisplayName}");
                _inventorApp.Visible = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao abrir documento: {ex.Message}");
                throw;
            }
        }

        public string? GetCustomIProperty(dynamic document, string propertyName)
        {
            try
            {
                dynamic customPropertySet = document.PropertySets["User Defined Properties"];
                foreach (dynamic prop in customPropertySet)
                {
                    if (prop.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"✅ iProperty customizada '{propertyName}' encontrada com valor: '{prop.Value}'");
                        return prop.Value?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Não foi possível encontrar a iProperty customizada '{propertyName}'. Isso pode ser esperado. Erro: {ex.Message}");
            }

            Console.WriteLine($"[WARNING] iProperty customizada '{propertyName}' não encontrada no documento {document.FullFileName}.");
            return null;
        }

        public bool ActivateDocument(string fileName)
        {
            try
            {
                if (_inventorApp?.Documents == null) throw new InvalidOperationException("Inventor não conectado.");

                Console.WriteLine($"🎯 Ativando documento: {fileName}");
                for (int i = 1; i <= _inventorApp.Documents.Count; i++)
                {
                    try
                    {
                        dynamic doc = _inventorApp.Documents[i];
                        if (doc.DisplayName.Equals(fileName, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(doc.FullFileName ?? "").Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            _inventorApp.ActiveDocument = doc;
                            _inventorApp.Visible = true;
                            Console.WriteLine($"✅ Documento ativado: {doc.DisplayName}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Erro ao verificar documento {i}: {ex.Message}");
                    }
                }
                throw new FileNotFoundException($"Documento '{fileName}' não encontrado nos documentos abertos.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao ativar documento: {ex.Message}");
                throw;
            }
        }

        public ActiveDocumentInfo? GetActiveDocumentInfo()
        {
            try
            {
                if (_inventorApp?.ActiveDocument == null) return null;

                dynamic activeDoc = _inventorApp.ActiveDocument;
                return new ActiveDocumentInfo
                {
                    FileName = activeDoc.DisplayName,
                    FilePath = activeDoc.FullFileName ?? activeDoc.DisplayName,
                    DocumentType = GetDocumentTypeName(activeDoc.DocumentType),
                    IsSaved = !activeDoc.Dirty,
                    IsAssembly = activeDoc.DocumentType == 12291,
                    LastSaved = File.Exists(activeDoc.FullFileName) ? File.GetLastWriteTime(activeDoc.FullFileName) : (DateTime?)null
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao obter informações do documento ativo: {ex.Message}");
                return null;
            }
        }

        private string GetDocumentTypeName(int documentType)
        {
            return documentType switch
            {
                12290 => "Part (.ipt)",
                12291 => "Assembly (.iam)",
                12292 => "Drawing (.idw/.dwg)",
                12293 => "Presentation (.ipn)",
                _ => $"Unknown ({documentType})"
            };
        }

        private List<BomItem> ExtractBOM(dynamic assemblyDoc)
        {
            var bomItems = new List<BomItem>();
            try
            {
                Console.WriteLine("Acessando ComponentDefinition...");
                dynamic compDef = assemblyDoc.ComponentDefinition;
                Console.WriteLine("Acessando BOM...");
                dynamic bom = compDef.BOM;
                bom.StructuredViewEnabled = true;
                bom.StructuredViewFirstLevelOnly = false;
                Console.WriteLine("Obtendo vista estruturada...");
                dynamic bomView = bom.BOMViews["Structured"];
                int rowCount = bomView.BOMRows.Count;
                Console.WriteLine($"BOM estruturado obtido! Processando {rowCount} itens principais...");
                ProcessBOMRows(bomView.BOMRows, bomItems, 0);
                Console.WriteLine($"✓ {bomItems.Count} itens extraídos no total!");
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao extrair BOM: {ex.Message}", ex);
            }
            return bomItems;
        }

        private void ProcessBOMRows(dynamic bomRows, List<BomItem> bomItems, int level)
        {
            foreach (dynamic bomRow in bomRows)
            {
                try
                {
                    dynamic? componentOccurrence = null;
                    try { componentOccurrence = bomRow.ComponentOccurrence; } catch { }

                    dynamic? componentDefinition = null;
                    if (componentOccurrence != null)
                    {
                        componentDefinition = componentOccurrence.Definition;
                    }
                    else
                    {
                        try { componentDefinition = bomRow.ComponentDefinitions[1]; } catch { }
                    }

                    if (componentDefinition == null)
                    {
                        Console.WriteLine($"⚠️ Ignorando item sem ComponentDefinition válido: {GetSafeString(() => bomRow.ItemNumber?.ToString())} (Nível: {level})");
                        continue;
                    }

                    string partNumber = GetPartNumber(bomRow, componentDefinition);
                    string description = GetDescription(bomRow, componentDefinition);
                    string documentPath = GetDocumentPath(componentDefinition);
                    string material = GetMaterial(componentDefinition);
                    double mass = GetMass(componentDefinition);
                    double volume = GetVolume(componentDefinition);

                    var bomItem = new BomItem
                    {
                        Level = level,
                        ItemNumber = GetSafeString(() => bomRow.ItemNumber?.ToString()),
                        PartNumber = partNumber,
                        Description = description,
                        Quantity = GetSafeValue(() => bomRow.ItemQuantity),
                        Units = GetSafeString(() => bomRow.ItemQuantityUnits?.ToString()),
                        DocumentPath = documentPath,
                        Material = material,
                        Mass = mass,
                        Volume = volume
                    };
                    bomItems.Add(bomItem);

                    string indent = new string(' ', level * 2);
                    Console.WriteLine($"{indent}✓ {bomItem.PartNumber} (Qtd: {bomItem.Quantity}) - {bomItem.Description}");

                    try
                    {
                        if (bomRow.ChildRows != null && bomRow.ChildRows.Count > 0)
                        {
                            ProcessBOMRows(bomRow.ChildRows, bomItems, level + 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{indent}⚠️ Erro ao processar filhos para {bomItem.PartNumber}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro geral no item nível {level}: {ex.Message}");
                    bomItems.Add(new BomItem { Level = level, ItemNumber = "ERRO", PartNumber = "ERRO", Description = $"Erro ao processar item: {ex.Message}", Quantity = 0 });
                }
            }
        }

        private string GetSafeString(Func<string?> getter)
        {
            try { return getter() ?? ""; } catch { return ""; }
        }

        private object GetSafeValue(Func<object> getter)
        {
            try { return getter() ?? 0; } catch { return 0; }
        }

        private string GetPartNumber(dynamic bomRow, dynamic componentDefinition)
        {
            try
            {
                dynamic designProps = componentDefinition.Document.PropertySets["Design Tracking Properties"];
                string? partNumber = designProps["Part Number"].Value?.ToString();
                if (!string.IsNullOrEmpty(partNumber)) return partNumber;
            }
            catch { }

            try
            {
                string? fileName = componentDefinition.Document.DisplayName?.ToString();
                return !string.IsNullOrEmpty(fileName) ? Path.GetFileNameWithoutExtension(fileName) : "N/A";
            }
            catch { return "N/A"; }
        }

        private string GetDescription(dynamic bomRow, dynamic componentDefinition)
        {
            try
            {
                dynamic designProps = componentDefinition.Document.PropertySets["Design Tracking Properties"];
                return designProps["Description"].Value?.ToString() ?? "Sem descrição";
            }
            catch { return "Sem descrição"; }
        }

        private string GetDocumentPath(dynamic componentDefinition)
        {
            try { return componentDefinition.Document.FullFileName?.ToString() ?? "N/A"; }
            catch { return "N/A"; }
        }

        private string GetMaterial(dynamic componentDefinition)
        {
            try
            {
                if (componentDefinition.Document.DocumentType == 12290)
                {
                    dynamic material = componentDefinition.Material;
                    return material?.DisplayName?.ToString() ?? "Material não definido";
                }
                return "N/A";
            }
            catch { return "N/A"; }
        }

        private double GetMass(dynamic componentDefinition)
        {
            try { return Convert.ToDouble(componentDefinition.MassProperties.Mass); }
            catch { return 0.0; }
        }

        private double GetVolume(dynamic componentDefinition)
        {
            try { return Convert.ToDouble(componentDefinition.MassProperties.Volume); }
            catch { return 0.0; }
        }

        public void ExportBOMToCSV(List<BomItem> bomItems, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.WriteLine("Nível,Item,Peça,Descrição,Quantidade,Unidades,Material,Massa,Volume,Caminho");
            foreach (var item in bomItems)
            {
                writer.WriteLine($"{item.Level}," + $"\"{item.ItemNumber}\"," + $"\"{item.PartNumber}\"," + $"\"{item.Description}\"," + $"{item.Quantity}," + $"\"{item.Units}\"," + $"\"{item.Material}\"," + $"{item.Mass:F3}," + $"{item.Volume:F6}," + $"\"{item.DocumentPath}\"");
            }
        }

        public void ExportBOMToExcel(List<BomItem> bomItems, string filePath)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("BOM");

            var headers = new[] { "Nível", "Item", "Peça", "Descrição", "Qtd", "Unidades", "Material", "Massa(kg)", "Volume(cm³)", "Arquivo" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            using (var range = worksheet.Cells[1, 1, 1, headers.Length])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }

            for (int i = 0; i < bomItems.Count; i++)
            {
                var item = bomItems[i];
                int row = i + 2;
                worksheet.Cells[row, 1].Value = item.Level;
                worksheet.Cells[row, 2].Value = item.ItemNumber;
                worksheet.Cells[row, 3].Value = item.PartNumber;
                worksheet.Cells[row, 4].Value = item.Description;
                worksheet.Cells[row, 5].Value = item.Quantity;
                worksheet.Cells[row, 6].Value = item.Units;
                worksheet.Cells[row, 7].Value = item.Material;
                worksheet.Cells[row, 8].Value = item.Mass;
                worksheet.Cells[row, 9].Value = item.Volume;
                worksheet.Cells[row, 10].Value = item.DocumentPath;
                worksheet.Cells[row, 2].Style.Indent = item.Level;

                if (i % 2 == 0)
                {
                    using (var range = worksheet.Cells[row, 1, row, headers.Length])
                    {
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }
                }
            }

            worksheet.Cells.AutoFitColumns();
            worksheet.Cells[bomItems.Count + 3, 1].Value = "Gerado em:";
            worksheet.Cells[bomItems.Count + 3, 2].Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            worksheet.Cells[bomItems.Count + 4, 1].Value = "Total:";
            worksheet.Cells[bomItems.Count + 4, 2].Value = bomItems.Count;
            package.SaveAs(new FileInfo(filePath));
        }

        public string? GetInventorVersion()
        {
            try
            {
                if (_inventorApp != null) return _inventorApp.SoftwareVersion.DisplayVersion?.ToString();
                return null;
            }
            catch { return null; }
        }

        public dynamic? GetInventorApp()
        {
            try
            {
                if (_inventorApp != null) return _inventorApp;
                ConnectToInventor();
                return _inventorApp;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter aplicação Inventor: {ex.Message}");
                return null;
            }
        }

        public bool IsInventorAppValid()
        {
            try
            {
                if (_inventorApp == null) return false;
                var version = _inventorApp.SoftwareVersion;
                return version != null;
            }
            catch { return false; }
        }
    }

    // Esta classe é a definição local usada pelos métodos de extração.
    // O método ExtractBOMAsync irá mapear desta para a classe em Models.
    public class BomItem
    {
        public int Level { get; set; }
        public string ItemNumber { get; set; } = "";
        public string PartNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public object Quantity { get; set; } = 0;
        public string Units { get; set; } = "";
        public string DocumentPath { get; set; } = "";
        public string Material { get; set; } = "";
        public double Mass { get; set; }
        public double Volume { get; set; }
    }
}