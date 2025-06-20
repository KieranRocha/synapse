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

    private readonly ILogger<WorkDrivenMonitoringService> _logger;
    private readonly IInventorDocumentEventService _documentEventService;
    private readonly DocumentProcessingService _documentProcessingService;
    private readonly WorkSessionService _workSessionService;
    private readonly CompanionConfiguration _configuration;

    // Serviços injetados que são necessários para a nova lógica
    private readonly IInventorConnectionService _inventorConnection;
    private readonly IInventorBOMExtractor _bomExtractor;
    private readonly IApiCommunicationService _apiService;

    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DocumentWatcher> _documentWatchers = new();
    private bool _isMonitoring = false;

    public WorkDrivenMonitoringService(
        ILogger<WorkDrivenMonitoringService> logger,
        IOptions<CompanionConfiguration> configuration,
        IInventorDocumentEventService documentEventService,
        DocumentProcessingService documentProcessingService,
        WorkSessionService workSessionService,
        IInventorConnectionService inventorConnection, // Adicionado
        IInventorBOMExtractor bomExtractor,           // Adicionado
        IApiCommunicationService apiService)           // Adicionado
    {
        _logger = logger;
        _configuration = configuration.Value;
        _documentEventService = documentEventService;
        _documentProcessingService = documentProcessingService;
        _workSessionService = workSessionService;
        _inventorConnection = inventorConnection;
        _bomExtractor = bomExtractor;
        _apiService = apiService;

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

    private async void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        try
        {
            _logger.LogInformation($"📂 Documento aberto: {e.FileName}");

            // --- Lógica para identificar a máquina ---
            var inventorApp = _inventorConnection.GetInventorApp();
            if (inventorApp != null && e.DocumentType == DocumentType.Assembly)
            {
                dynamic? doc = null;
                try
                {
                    // Encontra o objeto do documento que acabou de ser aberto
                    doc = inventorApp.Documents[e.FilePath];
                }
                catch (Exception) { /* Ignora se não encontrar, pode acontecer em alguns cenários */ }

                if (doc != null)
                {
                    var machineIdStr = _bomExtractor.GetCustomIProperty(doc, "MachineDB_ID");
                    if (!string.IsNullOrEmpty(machineIdStr) && int.TryParse(machineIdStr, out int machineId))
                    {
                        _logger.LogInformation("Montagem principal da Máquina ID {MachineId} aberta: {FileName}", machineId, e.FileName);

                        // Mapeia o caminho do arquivo ao ID da máquina
                        _fileToMachineIdMap[e.FilePath] = machineId;

                        // Notifica o servidor que a máquina está em "Design"
                        await _apiService.UpdateMachineStatusAsync(machineId, "Design", Environment.UserName, e.FileName);
                    }
                }
            }
            // --- Fim da lógica de máquina ---

            var documentEvent = CreateDocumentEvent(e.FilePath, e.FileName, DocumentEventType.Opened, e.DocumentType);

            // Detecta projeto
            var projectInfo = DetectProjectFromFile(e.FilePath);
            if (projectInfo != null)
            {
                documentEvent.ProjectId = projectInfo.ProjectId;
                documentEvent.ProjectName = projectInfo.DetectedName;
            }

            // Inicia sessão de trabalho
            var workSession = new WorkSession
            {
                FilePath = e.FilePath,
                FileName = e.FileName,
                ProjectId = projectInfo?.ProjectId,
                ProjectName = projectInfo?.DetectedName,
                Engineer = Environment.UserName
            };

            await _workSessionService.StartWorkSessionAsync(workSession);

            // Cria watcher para o documento
            CreateDocumentWatcher(e.FilePath, workSession);

            // Processa evento
            await _documentProcessingService.ProcessDocumentChangeAsync(documentEvent);
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

            // Tenta obter o MachineId do mapa para enriquecer o evento
            _fileToMachineIdMap.TryGetValue(e.FilePath, out int machineId);

            var documentEvent = CreateDocumentEvent(e.FilePath, e.FileName, DocumentEventType.Saved, e.DocumentType);

            // Adiciona o machineId ao evento se ele existir no mapa
            if (machineId > 0)
            {
                // Supondo que você adicione uma propriedade MachineId em DocumentEvent
                // documentEvent.MachineId = machineId;
            }

            // Detecta projeto
            var projectInfo = DetectProjectFromFile(e.FilePath);
            if (projectInfo != null)
            {
                documentEvent.ProjectId = projectInfo.ProjectId;
                documentEvent.ProjectName = projectInfo.DetectedName;
            }

            // Processa save (inclui extração de BOM se for assembly)
            await _documentProcessingService.ProcessDocumentSaveAsync(documentEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Erro ao processar save de documento: {e.FileName}");
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