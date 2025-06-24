// Services/InventorDocumentEventService.cs - VERSÃO FINAL CORRIGIDA
using Microsoft.Extensions.Logging;
using CADCompanion.Agent.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace CADCompanion.Agent.Services
{
    public interface IInventorDocumentEventService
    {
        event EventHandler<DocumentOpenedEventArgs>? DocumentOpened;
        event EventHandler<DocumentClosedEventArgs>? DocumentClosed;
        event EventHandler<DocumentSavedEventArgs>? DocumentSaved;

        Task<bool> SubscribeToDocumentEventsAsync();
        Task UnsubscribeFromDocumentEventsAsync();
        bool IsSubscribed { get; }
    }

    public class InventorDocumentEventService : IInventorDocumentEventService
    {
        private readonly ILogger<InventorDocumentEventService> _logger;
        private readonly IInventorConnectionService _inventorConnection;
        private bool _isSubscribed = false;

        // ✅ CORREÇÃO 1: Especifica System.Threading.Timer explicitamente
        private System.Threading.Timer? _documentPollingTimer;
        private readonly Dictionary<string, DateTime> _lastKnownDocuments = new();

        // Eventos públicos
        public event EventHandler<DocumentOpenedEventArgs>? DocumentOpened;
        public event EventHandler<DocumentClosedEventArgs>? DocumentClosed;
        public event EventHandler<DocumentSavedEventArgs>? DocumentSaved;

        public bool IsSubscribed => _isSubscribed;

        public InventorDocumentEventService(
            ILogger<InventorDocumentEventService> logger,
            IInventorConnectionService inventorConnection)
        {
            _logger = logger;
            _inventorConnection = inventorConnection;
        }

        public async Task<bool> SubscribeToDocumentEventsAsync()
        {
            try
            {
                if (_isSubscribed)
                {
                    _logger.LogWarning("Já subscrito aos eventos do Inventor");
                    return true;
                }

                if (!_inventorConnection.IsConnected)
                {
                    _logger.LogError("Inventor não conectado - não é possível subscrever eventos");
                    return false;
                }

                _logger.LogInformation("🔄 Iniciando monitoramento via polling (mais confiável que eventos COM)");

                // Detecta documentos já abertos
                await DetectAlreadyOpenDocumentsAsync();

                // ✅ CORREÇÃO 2: Especifica System.Threading.Timer explicitamente
                _documentPollingTimer = new System.Threading.Timer(PollDocumentChanges, null,
                    TimeSpan.FromSeconds(2), // Initial delay
                    TimeSpan.FromSeconds(3)  // Interval - check every 3 seconds
                );

                _isSubscribed = true;
                _logger.LogInformation("✅ Monitoring de documentos ativo via polling");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao subscrever eventos do Inventor");
                return false;
            }
        }

        public Task UnsubscribeFromDocumentEventsAsync()
        {
            try
            {
                _documentPollingTimer?.Dispose();
                _documentPollingTimer = null;

                _lastKnownDocuments.Clear();
                _isSubscribed = false;

                _logger.LogInformation("🔌 Desconectado do monitoring de documentos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao desconectar monitoring de documentos");
            }

            return Task.CompletedTask;
        }

        #region Polling-Based Document Detection

        private async void PollDocumentChanges(object? state)
        {
            try
            {
                if (!_inventorConnection.IsConnected)
                    return;

                await Task.Run(() => DetectDocumentChanges());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no polling de documentos");
            }
        }

        private void DetectDocumentChanges()
        {
            try
            {
                var inventorApp = _inventorConnection.GetInventorApp();
                if (inventorApp == null)
                    return;

                var currentDocuments = new Dictionary<string, DateTime>();

                // Itera pelos documentos atualmente abertos
                try
                {
                    var documents = inventorApp.Documents;
                    var count = documents.Count;

                    for (int i = 1; i <= count; i++)
                    {
                        try
                        {
                            var doc = documents[i];
                            var filePath = doc.FullFileName?.ToString() ?? string.Empty;

                            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                            {
                                var lastWrite = File.GetLastWriteTime(filePath);
                                currentDocuments[filePath] = lastWrite;

                                // Verifica se é um novo documento
                                if (!_lastKnownDocuments.ContainsKey(filePath))
                                {
                                    FireDocumentOpened(filePath, doc);
                                }
                                // Verifica se foi modificado (saved)
                                else if (_lastKnownDocuments[filePath] != lastWrite)
                                {
                                    FireDocumentSaved(filePath, doc);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Erro ao processar documento {i}");
                        }
                    }

                    // Detecta documentos fechados
                    var closedDocuments = _lastKnownDocuments.Keys
                        .Where(path => !currentDocuments.ContainsKey(path))
                        .ToList();

                    foreach (var closedPath in closedDocuments)
                    {
                        FireDocumentClosed(closedPath);
                        _lastKnownDocuments.Remove(closedPath);
                    }

                    // Atualiza o cache de documentos conhecidos
                    _lastKnownDocuments.Clear();
                    foreach (var kvp in currentDocuments)
                    {
                        _lastKnownDocuments[kvp.Key] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao acessar coleção de documentos");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na detecção de mudanças de documentos");
            }
        }

        private async Task DetectAlreadyOpenDocumentsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var inventorApp = _inventorConnection.GetInventorApp();
                    if (inventorApp == null) return;

                    try
                    {
                        var documents = inventorApp.Documents;
                        var count = documents.Count;

                        for (int i = 1; i <= count; i++)
                        {
                            try
                            {
                                var doc = documents[i];
                                var filePath = doc.FullFileName?.ToString() ?? string.Empty;

                                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                                {
                                    var lastWrite = File.GetLastWriteTime(filePath);
                                    _lastKnownDocuments[filePath] = lastWrite;

                                    FireDocumentOpened(filePath, doc);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"Erro ao processar documento {i}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao acessar coleção de documentos");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao detectar documentos já abertos");
            }
        }

        #endregion

        #region Event Firing Methods

        private void FireDocumentOpened(string filePath, dynamic doc)
        {
            try
            {
                var fileName = doc?.DisplayName?.ToString() ?? Path.GetFileName(filePath);

                var eventArgs = new DocumentOpenedEventArgs
                {
                    FilePath = filePath,
                    FileName = fileName,
                    DocumentType = DetermineDocumentType(filePath), // ✅ Agora retorna enum
                    Timestamp = DateTime.UtcNow,
                    FileSizeBytes = GetFileSize(filePath)
                };

                _logger.LogInformation($"📂 ABERTO: {fileName}");
                DocumentOpened?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao disparar evento DocumentOpened: {filePath}");
            }
        }

        private void FireDocumentClosed(string filePath)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);

                var eventArgs = new DocumentClosedEventArgs
                {
                    FilePath = filePath,
                    FileName = fileName,
                    DocumentType = DetermineDocumentType(filePath), // ✅ Agora retorna enum
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation($"📂 FECHADO: {fileName}");
                DocumentClosed?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao disparar evento DocumentClosed: {filePath}");
            }
        }

        private void FireDocumentSaved(string filePath, dynamic doc)
        {
            try
            {
                var fileName = doc?.DisplayName?.ToString() ?? Path.GetFileName(filePath);

                var eventArgs = new DocumentSavedEventArgs
                {
                    FilePath = filePath,
                    FileName = fileName,
                    DocumentType = DetermineDocumentType(filePath),
                    Timestamp = DateTime.UtcNow,
                    IsAutoSave = false // Removido FileSizeBytes - não existe nesta classe
                };

                _logger.LogInformation($"💾 SALVO: {fileName}");
                DocumentSaved?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao disparar evento DocumentSaved: {filePath}");
            }
        }

        #endregion

        #region Helper Methods

        // ✅ CORREÇÃO 3: Método agora retorna enum DocumentType em vez de string
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
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}