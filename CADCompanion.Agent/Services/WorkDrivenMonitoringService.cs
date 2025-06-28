using CADCompanion.Agent.Configuration;
using CADCompanion.Agent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CADCompanion.Agent.Services;

public class WorkDrivenMonitoringService : IWorkDrivenMonitoringService, IDisposable
{
    // Mapa para associar um arquivo aberto ao ID da máquina correspondente
    private readonly ConcurrentDictionary<string, int> _fileToMachineIdMap = new();

    // Dicionário para armazenar o hash do BOM por arquivo aberto
    private readonly ConcurrentDictionary<string, string> _lastBomHashByFile = new();

    private readonly ILogger<WorkDrivenMonitoringService> _logger;
    private readonly IInventorDocumentEventService _documentEventService;
    private readonly DocumentProcessingService _documentProcessingService;
    private readonly WorkSessionService _workSessionService;
    private readonly CompanionConfiguration _configuration;
    private readonly INotificationService _notificationService; // ✅ ADICIONADO

    // Serviços injetados que são necessários para a nova lógica
    private readonly IInventorConnectionService _inventorConnection;
    private readonly IInventorBOMExtractor _bomExtractor;
    private readonly IApiCommunicationService _apiService;

    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DocumentWatcher> _documentWatchers = new();
    private bool _isMonitoring = false;
    private DateTime _lastDocumentOpenTime = DateTime.MinValue;
    public WorkDrivenMonitoringService(
        ILogger<WorkDrivenMonitoringService> logger,
        IOptions<CompanionConfiguration> configuration,
        IInventorDocumentEventService documentEventService,
        DocumentProcessingService documentProcessingService,
        WorkSessionService workSessionService,
        IInventorConnectionService inventorConnection,
        IInventorBOMExtractor bomExtractor,
        IApiCommunicationService apiService,
        INotificationService notificationService) // ✅ ADICIONADO
    {
        _logger = logger;
        _configuration = configuration.Value;
        _documentEventService = documentEventService;
        _documentProcessingService = documentProcessingService;
        _workSessionService = workSessionService;
        _inventorConnection = inventorConnection;
        _bomExtractor = bomExtractor;
        _apiService = apiService;
        _notificationService = notificationService; // ✅ ADICIONADO

        // Subscreve aos eventos de documentos
        SubscribeToDocumentEvents();
    }

    public void StartMonitoring()
    {
        try
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Monitoramento já está ativo");
                return;
            }

            _logger.LogInformation("🚀 Iniciando Work-Driven Monitoring...");

            // Inicia monitoramento de eventos do Inventor
            _ = Task.Run(async () => await _documentEventService.SubscribeToDocumentEventsAsync());

            // Inicia watchers de pastas (se configurado)
            InitializeFileSystemWatchers();

            _isMonitoring = true;
            _logger.LogInformation("✅ Work-Driven Monitoring ativo");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar monitoramento");
            throw;
        }
    }

    public void StopMonitoring()
    {
        try
        {
            _logger.LogInformation("🛑 Parando Work-Driven Monitoring...");

            // Para eventos do Inventor
            _ = Task.Run(async () => await _documentEventService.UnsubscribeFromDocumentEventsAsync());

            // Para watchers de arquivos
            StopFileSystemWatchers();

            // Finaliza sessões ativas
            _ = Task.Run(async () => await FinalizeActiveSessionsAsync());

            _isMonitoring = false;
            _logger.LogInformation("✅ Work-Driven Monitoring parado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar monitoramento");
        }
    }

    #region Document Event Handling

    private void SubscribeToDocumentEvents()
    {
        _documentEventService.DocumentOpened += OnDocumentOpened;
        _documentEventService.DocumentClosed += OnDocumentClosed;
        _documentEventService.DocumentSaved += OnDocumentSaved;
    }

    private readonly HashSet<string> _processedFiles = new();

    private async void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastOpen = (now - _lastDocumentOpenTime).TotalSeconds;
        try
        {
            // Evita processar o mesmo arquivo múltiplas vezes
            lock (_processedFiles)
            {
                if (_processedFiles.Contains(e.FilePath))
                    return;
                _processedFiles.Add(e.FilePath);
            }

            _logger.LogInformation($"📂 Documento aberto: {e.FileName}");

            // ✅ NOTIFICAÇÃO PARA ABERTURA DE DOCUMENTO
            if (timeSinceLastOpen > 3)
            {
                var fileName = e.FileName ?? Path.GetFileName(e.FilePath);
                var documentType = e.DocumentType.ToString();
                // A validação principal será feita abaixo, após obter o doc do Inventor
            }

            _lastDocumentOpenTime = now;

            // --- Lógica para identificar a máquina e OP ---
            var inventorApp = _inventorConnection.GetInventorApp();
            if (inventorApp != null && e.DocumentType == DocumentType.Assembly)
            {
                await Task.Delay(2000);
                dynamic? doc = null;
                try
                {
                    // Encontra o objeto do documento que acabou de ser aberto
                    foreach (dynamic document in inventorApp.Documents)
                    {
                        if (document.FullFileName == e.FilePath)
                        {
                            doc = document;
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    /* Ignora se não encontrar, pode acontecer em alguns cenários de tempo */
                }

                if (doc != null)
                {
                    var machineIdStr = _bomExtractor.GetCustomIProperty(doc, "MachineDB_ID");
                    var opStr = _bomExtractor.GetCustomIProperty(doc, "OP");

                    bool hasMachineId = !string.IsNullOrWhiteSpace(machineIdStr);
                    bool hasOp = !string.IsNullOrWhiteSpace(opStr);

                    if (hasMachineId && hasOp)
                    {
                        int machineId = int.Parse(machineIdStr);
                        var machineInfo = await _apiService.GetMachineAsync(machineId);
                        if (machineInfo != null)
                        {
                            // ✅ ADICIONAR: Armazenar MachineId no mapeamento
                            _fileToMachineIdMap[e.FilePath] = machineId;
                            
                            // Log detalhado das propriedades da máquina
                            Console.WriteLine("[DEBUG] Propriedades da máquina carregadas do banco:");
                            Console.WriteLine($"  Id: {machineInfo.Id}");
                            Console.WriteLine($"  Name: {machineInfo.Name}");
                            Console.WriteLine($"  OperationNumber: {machineInfo.OperationNumber}");
                            Console.WriteLine($"  ProjectId: {machineInfo.ProjectId}");
                            Console.WriteLine($"  ProjectName: {machineInfo.ProjectName}");
                            Console.WriteLine($"  Status: {machineInfo.Status}");
                            Console.WriteLine($"  Description: {machineInfo.Description}");
                            // Adicione mais propriedades conforme necessário
                            _logger.LogInformation("🔧 Máquina encontrada - ID: {Id}, Nome: {Name}, OP: {OperationNumber}, Projeto: {ProjectId}",
                                machineInfo.Id, machineInfo.Name, machineInfo.OperationNumber, machineInfo.ProjectId);

                            if (!string.Equals(opStr, machineInfo.OperationNumber, StringComparison.OrdinalIgnoreCase))
                            {
                                if (timeSinceLastOpen > 3)
                                {
                                    await _notificationService.ShowWarningAsync(
                                        "Inconsistência de OP",
                                        $"A OP da iProperty ('{opStr}') é diferente da OP da máquina no sistema ('{machineInfo.OperationNumber}')."
                                    );
                                }
                            }
                            else if (timeSinceLastOpen > 3)
                            {
                                await _notificationService.ShowInfoAsync(
                                    "Máquina Aberta",
                                    $"OP: {machineInfo.OperationNumber} - {machineInfo.Name}\nProjeto: {machineInfo.ProjectName}"
                                );
                            }
                        }
                        else
                        {
                            _logger.LogWarning("❌ Máquina ID {MachineId} não encontrada no servidor", machineId);
                            if (timeSinceLastOpen > 3)
                            {
                                await _notificationService.ShowInfoAsync(
                                    "Máquina Aberta",
                                    $"Máquina ID: {machineId} - {e.FileName}"
                                );
                            }
                        }
                    }
                    else if (hasMachineId && !hasOp)
                    {
                        int machineId = int.Parse(machineIdStr);
                        var machineInfo = await _apiService.GetMachineAsync(machineId);
                        string machineName = machineInfo?.Name ?? $"ID: {machineId}";
                        
                        // ✅ ADICIONAR: Armazenar MachineId mesmo sem OP
                        if (machineInfo != null)
                        {
                            _fileToMachineIdMap[e.FilePath] = machineId;
                        }
                        
                        if (timeSinceLastOpen > 3)
                        {
                            await _notificationService.ShowWarningAsync(
                                "Atenção",
                                $"Máquina: {machineName} não possui OP definida."
                            );
                        }
                    }
                    else if (!hasMachineId && hasOp)
                    {
                        if (timeSinceLastOpen > 3)
                        {
                            await _notificationService.ShowWarningAsync(
                                "Atenção",
                                $"OP: {opStr} não possui máquina definida."
                            );
                        }
                    }
                    // Se não tem nenhum dos dois, não é montagem principal, segue fluxo normal sem notificação extra
                }
                // --- Fim da lógica de máquina/OP ---

                var documentEvent = CreateDocumentEvent(e.FilePath, e.FileName, DocumentEventType.Opened, e.DocumentType);

                var projectInfo = DetectProjectFromFile(e.FilePath);
                if (projectInfo != null)
                {
                    documentEvent.ProjectId = projectInfo.ProjectId;
                    documentEvent.ProjectName = projectInfo.DetectedName;
                }

                var workSession = new WorkSession
                {
                    FilePath = e.FilePath,
                    FileName = e.FileName,
                    ProjectId = projectInfo?.ProjectId,
                    ProjectName = projectInfo?.DetectedName,
                    Engineer = Environment.UserName
                };

                await _workSessionService.StartWorkSessionAsync(workSession);
                CreateDocumentWatcher(e.FilePath, workSession);
                await _documentProcessingService.ProcessDocumentChangeAsync(documentEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar abertura de documento: {e.FileName}");
        }
    }

    private async void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        try
        {
            _logger.LogInformation($"📂 Documento fechado: {e.FileName}");
            lock (_processedFiles)
            {
                _processedFiles.Remove(e.FilePath);
            }

            // --- Lógica para limpar o mapeamento da máquina ---
            if (_fileToMachineIdMap.TryRemove(e.FilePath, out int machineId))
            {
                _logger.LogInformation("Arquivo da Máquina ID {MachineId} foi fechado: {FileName}", machineId, e.FileName);
                // Opcional: notificar o servidor que a máquina não está mais "em uso"
                // await _apiService.UpdateMachineStatusAsync(machineId, "Available", Environment.UserName, string.Empty);
            }
            // --- Fim da lógica de máquina ---

            var documentEvent = CreateDocumentEvent(e.FilePath, e.FileName, DocumentEventType.Closed, e.DocumentType);

            // Remove watcher do documento
            RemoveDocumentWatcher(e.FilePath);

            // Finaliza sessão de trabalho (implementação futura)

            // Processa evento
            await _documentProcessingService.ProcessDocumentChangeAsync(documentEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar fechamento de documento: {e.FileName}");
        }
    }

    private async void OnDocumentSaved(object? sender, DocumentSavedEventArgs e)
    {
        try
        {
            _logger.LogDebug($"💾 Documento salvo: {e.FileName}");

            _fileToMachineIdMap.TryGetValue(e.FilePath, out int machineId);
            var documentEvent = CreateDocumentEvent(e.FilePath, e.FileName, DocumentEventType.Saved, e.DocumentType);
            if (machineId > 0)
            {
                _logger.LogInformation("Documento salvo pertence à Máquina ID: {MachineId}", machineId);
                // ✅ Adiciona MachineId ao DocumentEvent para uso posterior
                documentEvent.Engineer = machineId.ToString(); // Usar campo temporário para transportar MachineId
            }
            var projectInfo = DetectProjectFromFile(e.FilePath);
            if (projectInfo != null)
            {
                documentEvent.ProjectId = projectInfo.ProjectId;
                documentEvent.ProjectName = projectInfo.DetectedName;
            }

            // --- NOVA LÓGICA: Só exporta/processa BOM se mudou ---
            if (e.DocumentType == DocumentType.Assembly)
            {
                var inventorApp = _inventorConnection.GetInventorApp();
                if (inventorApp != null)
                {
                    dynamic? doc = null;
                    try
                    {
                        foreach (dynamic document in inventorApp.Documents)
                        {
                            if (document.FullFileName == e.FilePath)
                            {
                                doc = document;
                                break;
                            }
                        }
                    }
                    catch { }
                    if (doc != null)
                    {
                        var bomItems = _bomExtractor.GetBOMFromDocument(doc);
                        var bomHash = _bomExtractor.GetBOMHash(bomItems);
                        _lastBomHashByFile.TryGetValue(e.FilePath, out var lastHash);
                        if (lastHash != bomHash)
                        {
                            _lastBomHashByFile[e.FilePath] = bomHash;
                            // Só processa/exporta BOM se mudou
                            await _documentProcessingService.ProcessDocumentSaveAsync(documentEvent);
                        }
                        else
                        {
                            _logger.LogInformation($"BOM não mudou para {e.FileName}, não será exportado novamente.");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar save do documento: {e.FileName}");
        }
    }

    #endregion

    #region File System Watchers

    private void InitializeFileSystemWatchers()
    {
        if (_configuration.MonitoredFolders == null || !_configuration.MonitoredFolders.Any())
        {
            _logger.LogInformation("Nenhuma pasta configurada para monitoramento de arquivos");
            return;
        }

        foreach (var folder in _configuration.MonitoredFolders)
        {
            if (Directory.Exists(folder.Path))
            {
                var watcher = new FileSystemWatcher(folder.Path)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = folder.IncludeSubdirectories,
                    EnableRaisingEvents = true
                };

                foreach (var fileType in folder.FileTypes)
                {
                    watcher.Filter = fileType;
                }

                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileCreated;
                watcher.Deleted += OnFileDeleted;
                watcher.Renamed += OnFileRenamed;

                _watchers.TryAdd(folder.Path, watcher);
                _logger.LogInformation($"📁 Monitorando pasta: {folder.Path}");
            }
            else
            {
                _logger.LogWarning($"Pasta não existe: {folder.Path}");
            }
        }
    }

    private void StopFileSystemWatchers()
    {
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (IsCADFile(e.FullPath))
            {
                await ProcessFileEvent(e.FullPath, DocumentEventType.Modified);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar mudança de arquivo: {e.FullPath}");
        }
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (IsCADFile(e.FullPath))
            {
                await ProcessFileEvent(e.FullPath, DocumentEventType.Opened);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar criação de arquivo: {e.FullPath}");
        }
    }

    private async void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (IsCADFile(e.FullPath))
            {
                await ProcessFileEvent(e.FullPath, DocumentEventType.Closed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar exclusão de arquivo: {e.FullPath}");
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogInformation($"📄 Arquivo renomeado: {e.OldFullPath} → {e.FullPath}");
    }

    #endregion

    #region Document Watchers

    private void CreateDocumentWatcher(string filePath, WorkSession workSession)
    {
        try
        {
            var documentWatcher = new DocumentWatcher
            {
                FilePath = filePath,
                FileName = System.IO.Path.GetFileName(filePath),
                ProjectInfo = DetectProjectFromFile(filePath),
                OpenedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                WorkSessionId = workSession.Id
            };

            _documentWatchers.TryAdd(filePath, documentWatcher);
            _logger.LogDebug($"👁️ Watcher criado para: {documentWatcher.FileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao criar watcher para documento: {filePath}");
        }
    }

    private void RemoveDocumentWatcher(string filePath)
    {
        try
        {
            if (_documentWatchers.TryRemove(filePath, out var watcher))
            {
                watcher.Dispose();
                _logger.LogDebug($"👁️ Watcher removido para: {watcher.FileName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao remover watcher do documento: {filePath}");
        }
    }

    #endregion

    #region Helper Methods

    private DocumentEvent CreateDocumentEvent(string filePath, string fileName, DocumentEventType eventType, DocumentType docType)
    {
        return new DocumentEvent
        {
            FilePath = filePath,
            FileName = fileName,
            EventType = eventType,
            DocumentType = docType,
            Timestamp = DateTime.UtcNow,
            FileSizeBytes = GetFileSize(filePath),
            Engineer = Environment.UserName
        };
    }

    private ProjectInfo? DetectProjectFromFile(string filePath)
    {
        try
        {
            if (!_configuration.Settings.ProjectDetection.EnableAutoDetection)
                return null;

            var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var directoryPath = System.IO.Path.GetDirectoryName(filePath) ?? "";


            foreach (var pattern in _configuration.Settings.ProjectDetection.ProjectIdPatterns)
            {
                var regex = new System.Text.RegularExpressions.Regex(pattern);
                var match = regex.Match(fileName);

                if (match.Success && match.Groups.Count > 1)
                {
                    var projectId = match.Groups[1].Value;

                    return new ProjectInfo
                    {
                        ProjectId = projectId,
                        DetectedName = $"Projeto {projectId}",
                        FolderPath = directoryPath,
                        IsValidProject = true,
                        DetectedAt = DateTime.UtcNow
                    };
                }
            }

            if (_configuration.Settings.ProjectDetection.UnknownProjectHandling == "CREATE_UNKNOWN")
            {
                var unknownId = $"UNKNOWN_{DateTime.Now:yyyyMMdd}";
                return new ProjectInfo
                {
                    ProjectId = unknownId,
                    DetectedName = "Projeto Desconhecido",
                    FolderPath = directoryPath,
                    IsValidProject = false,
                    DetectedAt = DateTime.UtcNow
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao detectar projeto do arquivo: {filePath}");
            return null;
        }
    }

    private async Task ProcessFileEvent(string filePath, DocumentEventType eventType)
    {
        try
        {
            var fileName = System.IO.Path.GetFileName(filePath);
            var docType = DetermineDocumentType(filePath);

            var documentEvent = CreateDocumentEvent(filePath, fileName, eventType, docType);

            var projectInfo = DetectProjectFromFile(filePath);
            if (projectInfo != null)
            {
                documentEvent.ProjectId = projectInfo.ProjectId;
                documentEvent.ProjectName = projectInfo.DetectedName;
            }

            if (eventType == DocumentEventType.Saved)
            {
                await _documentProcessingService.ProcessDocumentSaveAsync(documentEvent);
            }
            else
            {
                await _documentProcessingService.ProcessDocumentChangeAsync(documentEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar evento de arquivo: {filePath}");
        }
    }

    private bool IsCADFile(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".iam" or ".ipt" or ".idw" or ".ipn";
    }

    private DocumentType DetermineDocumentType(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".iam" => DocumentType.Assembly,
            ".ipt" => DocumentType.Part,
            ".idw" => DocumentType.Drawing,
            ".ipn" => DocumentType.Presentation,
            _ => DocumentType.Unknown
        };
    }

    private long GetFileSize(string filePath)
    {
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                return new System.IO.FileInfo(filePath).Length;
            }
        }
        catch { /* Ignora erros */ }
        return 0;
    }

    private async Task FinalizeActiveSessionsAsync()
    {
        try
        {
            var activeSessions = await _workSessionService.GetActiveWorkSessionsAsync();
            foreach (var session in activeSessions)
            {
                await _workSessionService.EndWorkSessionAsync(session.Id, DateTime.UtcNow);
            }
            _logger.LogInformation($"Finalizadas {activeSessions.Count} sessões ativas");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar sessões ativas");
        }
    }

    #endregion

    public void Dispose()
    {
        StopMonitoring();

        foreach (var watcher in _documentWatchers.Values)
        {
            watcher.Dispose();
        }
        _documentWatchers.Clear();

        GC.SuppressFinalize(this);
    }
}