// Services/ApiCommunicationService.cs - CORRIGIDO

using CADCompanion.Agent.Models;
using CADCompanion.Shared.Contracts;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace CADCompanion.Agent.Services;

public class ApiCommunicationService : IApiCommunicationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiCommunicationService> _logger;

    public ApiCommunicationService(HttpClient httpClient, ILogger<ApiCommunicationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Método principal que envia a BOM para o servidor
    public async Task<bool> SubmitBomAsync(BomSubmissionDto bomData)
    {
        try
        {
            _logger.LogInformation("Enviando BOM para o servidor: {FilePath}", bomData.AssemblyFilePath);
            // Este é o endpoint correto que criamos no servidor
            var response = await _httpClient.PostAsJsonAsync("api/boms/submit", bomData);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("BOM enviada com sucesso para {FilePath}.", bomData.AssemblyFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha crítica ao enviar BOM para o servidor.");
            return false;
        }
    }

    // --- Implementações dos outros métodos da interface ---

    public async Task SendBOMDataAsync(BOMDataWithContext bomData)
    {
        try
        {
            _logger.LogInformation("Enviando dados de BOM: {AssemblyFileName}", bomData.AssemblyFileName);
            await _httpClient.PostAsJsonAsync("api/boms/data", bomData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar dados de BOM");
        }
    }

    public async Task SendDocumentActivityAsync(DocumentEvent documentEvent)
    {
        try
        {
            _logger.LogDebug("Enviando atividade de documento: {FileName}", documentEvent.FileName);
            await _httpClient.PostAsJsonAsync("api/activity/log", documentEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar atividade de documento");
        }
    }

    public async Task SendHeartbeatAsync()
    {
        try
        {
            _logger.LogDebug("Enviando Heartbeat");
            var heartbeat = new
            {
                CompanionId = Environment.MachineName,
                Timestamp = DateTime.UtcNow,
                Status = "RUNNING"
            };
            await _httpClient.PostAsJsonAsync("api/session/heartbeat", heartbeat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar heartbeat");
        }
    }

    public async Task SendPartDataAsync(object partData)
    {
        try
        {
            _logger.LogDebug("Enviando dados de peça");
            await _httpClient.PostAsJsonAsync("api/parts/submit", partData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar dados de peça");
        }
    }

    public async Task SendWorkSessionEndedAsync(WorkSession session)
    {
        try
        {
            _logger.LogInformation("Enviando fim da sessão de trabalho: {FileName}", session.FileName);
            await _httpClient.PostAsJsonAsync("api/session/end", session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar fim da sessão de trabalho");
        }
    }

    public async Task SendWorkSessionUpdatedAsync(WorkSession session)
    {
        try
        {
            _logger.LogDebug("Enviando atualização da sessão de trabalho: {FileName}", session.FileName);
            await _httpClient.PostAsJsonAsync("api/session/update", session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar atualização da sessão de trabalho");
        }
    }

    // Sobrecarga para compatibilidade com WorkSessionService
    public async Task SendWorkSessionUpdatedAsync(WorkSession session, string updateReason)
    {
        try
        {
            _logger.LogDebug("Enviando atualização da sessão de trabalho: {FileName} - Motivo: {UpdateReason}", 
                session.FileName, updateReason);
            
            var updateData = new
            {
                Session = session,
                UpdateReason = updateReason,
                Timestamp = DateTime.UtcNow
            };
            
            await _httpClient.PostAsJsonAsync("api/session/update", updateData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar atualização da sessão de trabalho");
        }
    }
}