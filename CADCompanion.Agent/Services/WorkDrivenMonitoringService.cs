// Services/WorkDrivenMonitoringService.cs - CORRIGIDO v2

using CADCompanion.Agent.Configuration;
using CADCompanion.Agent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using CADCompanion.Shared.Models; // Adicionado para encontrar ProjectInfo
namespace CADCompanion.Agent.Services;

public class WorkDrivenMonitoringService : IWorkDrivenMonitoringService, IDisposable
{
    private readonly ILogger<WorkDrivenMonitoringService> _logger;
    private readonly IInventorDocumentEventService _documentEventService;
    private readonly DocumentProcessingService _documentProcessingService;
    private readonly WorkSessionService _workSessionService;
    private readonly CompanionConfiguration _configuration;
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DocumentWatcher> _documentWatchers = new();
    private bool _isMonitoring = false;

    public WorkDrivenMonitoringService(
        ILogger<WorkDrivenMonitoringService> logger,
        IOptions<CompanionConfiguration> configuration,
        IInventorDocumentEventService documentEventService,
        DocumentProcessingService documentProcessingService,
        WorkSessionService workSessionService)
    {
        _logger = logger;
        _configuration = configuration.Value;
        _documentEventService = documentEventService;
        _documentProcessingService = documentProcessingService;
        _workSessionService = workSessionService;

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

            var documentEvent = CreateDocumentEvent(e.FilePath, e.FileName, DocumentEventType.Closed, e.DocumentType);

            // Remove watcher do documento
            RemoveDocumentWatcher(e.FilePath);

            // Finaliza sessão de trabalho
            // (será implementado quando tivermos o ID da sessão)

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

            var documentEvent = CreateDocumentEvent(e.FilePath, e.FileName, DocumentEventType.Saved, e.DocumentType);

            // Detecta projeto
            var projectInfo = DetectProjectFromFile(e.FilePath);
            if (projectInfo != null)
            {
                documentEvent.ProjectId = projectInfo.ProjectId;
                documentEvent.ProjectName = projectInfo.DetectedName;
            }

            // Atualiza sessão de trabalho
            // (implementar quando tivermos ID da sessão)

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

                // Adiciona filtros de extensão
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
                FileName = Path.GetFileName(filePath),
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

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var directoryPath = Path.GetDirectoryName(filePath) ?? "";

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

            // Se não encontrou padrão, cria projeto desconhecido
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
            var fileName = Path.GetFileName(filePath);
            var docType = DetermineDocumentType(filePath);

            var documentEvent = CreateDocumentEvent(filePath, fileName, eventType, docType);

            // Detecta projeto
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
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".iam" or ".ipt" or ".idw" or ".ipn";
    }

    private DocumentType DetermineDocumentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
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
            if (File.Exists(filePath))
            {
                return new FileInfo(filePath).Length;
            }
        }
        catch
        {
            // Ignora erros
        }
        return 0;
    }

    // ✅ CORRIGIDO: Método agora é async para resolver o warning
    private async Task FinalizeActiveSessionsAsync()
    {
        try
        {
            // Finaliza todas as sessões ativas
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

        // Dispose document watchers
        foreach (var watcher in _documentWatchers.Values)
        {
            watcher.Dispose();
        }
        _documentWatchers.Clear();

        GC.SuppressFinalize(this);
    }
}