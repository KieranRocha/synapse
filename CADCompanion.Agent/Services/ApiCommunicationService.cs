// Services/ApiCommunicationService.cs - CORRIGIDO DEFINITIVO

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

    // ✅ MÉTODO PRINCIPAL - Usa o endpoint correto
    public async Task<bool> SubmitBomAsync(BomSubmissionDto bomData)
    {
        try
        {
            _logger.LogInformation("Enviando BOM para o servidor: {FilePath}", bomData.AssemblyFilePath);
            // ✅ CORRETO: Usa o endpoint que existe no servidor
            var response = await _httpClient.PostAsJsonAsync("api/boms/submit", bomData);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("✅ BOM enviada com sucesso para {FilePath}.", bomData.AssemblyFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Falha ao enviar BOM para o servidor.");
            return false;
        }
    }

    // ✅ CORRIGIDO: Converte BOMDataWithContext para BomSubmissionDto
    public async Task SendBOMDataAsync(BOMDataWithContext bomData)
    {
        try
        {
            _logger.LogInformation("📤 Convertendo e enviando dados de BOM: {AssemblyFileName}", bomData.AssemblyFileName);

            // Converte BOMDataWithContext para BomSubmissionDto
            var bomSubmission = new BomSubmissionDto
            {
                ProjectId = bomData.ProjectId,
                MachineId = bomData.ExtractedBy, // Usa ExtractedBy como MachineId
                AssemblyFilePath = bomData.AssemblyFilePath,
                ExtractedBy = bomData.ExtractedBy,
                ExtractedAt = bomData.ExtractedAt,
                Items = bomData.BOMItems.Select(item => new BomItemDto
                {
                    PartNumber = item.PartNumber,
                    Description = item.Description,
                    Quantity = Convert.ToInt32(item.Quantity),
                    StockNumber = null,
                    // ✅ ADICIONAR campos faltantes:
                    Level = item.Level,
                    IsAssembly = item.DocumentPath.EndsWith(".iam", StringComparison.OrdinalIgnoreCase),
                    Material = item.Material,
                    Weight = item.Mass
                }).ToList()
            };

            // Usa o método principal que funciona
            var success = await SubmitBomAsync(bomSubmission);

            if (success)
            {
                _logger.LogInformation("✅ BOM convertida e enviada: {TotalItems} itens", bomData.TotalItems);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar dados de BOM");
        }
    }

    // ✅ CORRIGIDO: Cria endpoint no servidor ou apenas loga localmente
    public async Task SendDocumentActivityAsync(DocumentEvent documentEvent)
    {
        try
        {
            // Por enquanto, apenas loga localmente até criarmos o endpoint no servidor
            _logger.LogInformation("📝 Atividade de documento: {EventType} - {FileName}",
                documentEvent.EventType, documentEvent.FileName);

            // TODO: Implementar endpoint no servidor se necessário
            // await _httpClient.PostAsJsonAsync("api/activity/document", documentEvent);

            await Task.CompletedTask; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar atividade de documento");
        }
    }

    public async Task SendHeartbeatAsync()
    {
        try
        {
            _logger.LogDebug("💓 Enviando Heartbeat");
            var heartbeat = new
            {
                CompanionId = Environment.MachineName,
                Timestamp = DateTime.UtcNow,
                Status = "RUNNING"
            };
            // ✅ Este endpoint existe no servidor
            await _httpClient.PostAsJsonAsync("api/session/heartbeat", heartbeat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar heartbeat");
        }
    }
    public async Task UpdateMachineStatusAsync(int machineId, string status, string userName, string currentFile)
    {
        try
        {
            var payload = new { Status = status, UserName = userName, CurrentFile = currentFile };
            var response = await _httpClient.PostAsJsonAsync($"api/machines/{machineId}/status", payload);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Falha ao atualizar status da máquina {MachineId} no servidor. Status: {StatusCode}", machineId, response.StatusCode);
            }
            else
            {
                _logger.LogInformation("Status da máquina {MachineId} atualizado para {Status} no servidor.", machineId, status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro de comunicação ao tentar atualizar status da máquina {MachineId}.", machineId);
        }
    }
    public async Task SendPartDataAsync(object partData)
    {
        try
        {
            _logger.LogDebug("🔩 Enviando dados de peça");
            // TODO: Criar endpoint no servidor se necessário
            // await _httpClient.PostAsJsonAsync("api/parts/submit", partData);
            await Task.CompletedTask; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar dados de peça");
        }
    }

    public async Task SendWorkSessionEndedAsync(WorkSession session)
    {
        try
        {
            _logger.LogInformation("🏁 Enviando fim da sessão de trabalho: {FileName}", session.FileName);
            // TODO: Criar endpoint no servidor se necessário
            // await _httpClient.PostAsJsonAsync("api/session/end", session);
            await Task.CompletedTask; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar fim da sessão de trabalho");
        }
    }

    public async Task SendWorkSessionUpdatedAsync(WorkSession session)
    {
        try
        {
            _logger.LogDebug("🔄 Enviando atualização da sessão de trabalho: {FileName}", session.FileName);
            // TODO: Criar endpoint no servidor se necessário
            // await _httpClient.PostAsJsonAsync("api/session/update", session);
            await Task.CompletedTask; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar atualização da sessão de trabalho");
        }
    }

    // Sobrecarga para compatibilidade com WorkSessionService
    public async Task SendWorkSessionUpdatedAsync(WorkSession session, string updateReason)
    {
        try
        {
            _logger.LogDebug("🔄 Enviando atualização da sessão de trabalho: {FileName} - Motivo: {UpdateReason}",
                session.FileName, updateReason);

            var updateData = new
            {
                Session = session,
                UpdateReason = updateReason,
                Timestamp = DateTime.UtcNow
            };

            // TODO: Criar endpoint no servidor se necessário
            // await _httpClient.PostAsJsonAsync("api/session/update", updateData);
            await Task.CompletedTask; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar atualização da sessão de trabalho");
        }

    }
}