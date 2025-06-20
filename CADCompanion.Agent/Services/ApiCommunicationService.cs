using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;
using CADCompanion.Shared.Models;

namespace CADCompanion.Agent.Services
{
    /// <summary>
    /// Interface para comunicação com a API backend
    /// </summary>
    public interface IApiCommunicationService
    {
        Task<bool> TestConnectionAsync();
        Task<List<ProjectInfo>> GetActiveProjectsAsync();
        Task<bool> SendBomDataAsync(BOMDataWithContext bomData);
        Task SendActivityAsync(ActivityData activity);
        Task<bool> RegisterAgentAsync(AgentInfo agentInfo);

        // Métodos adicionais
        Task<bool> SubmitBomAsync(BomSubmissionDto bomSubmission);
        Task SendWorkSessionEndedAsync(WorkSession session);
        Task SendWorkSessionUpdatedAsync(WorkSession session);
        Task SendWorkSessionUpdatedAsync(WorkSession session, string updateReason);
        Task SendHeartbeatAsync();
        Task SendBOMDataAsync(BOMDataWithContext bomData);
        Task SendMachineStatusAsync(object machineStatus);
        Task SendPartDataAsync(object partData);
        Task SendDocumentActivityAsync(DocumentEvent documentEvent);
    }

    /// <summary>
    /// Implementação do serviço de comunicação com a API
    /// </summary>
    public class ApiCommunicationService : IApiCommunicationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiCommunicationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public ApiCommunicationService(
            HttpClient httpClient,
            ILogger<ApiCommunicationService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;

            // Configurar base address
            var apiUrl = _configuration["Api:BaseUrl"] ?? "http://localhost:5047";
            _httpClient.BaseAddress = new Uri(apiUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Configurar headers padrão
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CADCompanion.Agent/1.0");

            // Configurar opções de serialização JSON
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            // Configurar política de retry com Polly
            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} após {timespan} segundos");
                    });
        }

        /// <summary>
        /// Testa a conexão com a API
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.GetAsync("api/health"));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar conexão com API");
                return false;
            }
        }

        /// <summary>
        /// Obtém lista de projetos ativos
        /// </summary>
        public async Task<List<ProjectInfo>> GetActiveProjectsAsync()
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.GetAsync("api/projects/active"));

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<ProjectInfo>>(json, _jsonOptions)
                        ?? new List<ProjectInfo>();
                }

                _logger.LogWarning($"Erro ao obter projetos: {response.StatusCode}");
                return new List<ProjectInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter projetos ativos");
                return new List<ProjectInfo>();
            }
        }

        /// <summary>
        /// Envia dados de BOM extraídos para a API
        /// </summary>
        public async Task<bool> SendBomDataAsync(BOMDataWithContext bomData)
        {
            try
            {
                var json = JsonSerializer.Serialize(bomData, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.PostAsync("api/bom/versions", content));

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"BOM enviado com sucesso: {bomData.FileName}");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Erro ao enviar BOM: {response.StatusCode} - {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao enviar BOM: {bomData.FileName}");
                return false;
            }
        }

        /// <summary>
        /// Envia atividade para o feed
        /// </summary>
        public async Task SendActivityAsync(ActivityData activity)
        {
            try
            {
                var json = JsonSerializer.Serialize(activity, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("api/activities", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Erro ao enviar atividade: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar atividade");
                // Não propagar exceção para não interromper fluxo principal
            }
        }

        /// <summary>
        /// Registra o agent na API
        /// </summary>
        public async Task<bool> RegisterAgentAsync(AgentInfo agentInfo)
        {
            try
            {
                var json = JsonSerializer.Serialize(agentInfo, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.PostAsync("api/agents/register", content));

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Agent registrado com sucesso na API");
                    return true;
                }

                _logger.LogWarning($"Falha ao registrar agent: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao registrar agent");
                return false;
            }
        }

        /// <summary>
        /// Submete BOM com contexto adicional
        /// </summary>
        public async Task<bool> SubmitBomAsync(BomSubmissionDto bomSubmission)
        {
            try
            {
                var json = JsonSerializer.Serialize(bomSubmission, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.PostAsync($"api/projects/{bomSubmission.ProjectId}/machines/{bomSubmission.MachineId}/bom", content));

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao submeter BOM");
                return false;
            }
        }

        /// <summary>
        /// Envia finalização de sessão de trabalho
        /// </summary>
        public async Task SendWorkSessionEndedAsync(WorkSession session)
        {
            try
            {
                session.EndedAt = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(session, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync("api/sessions/end", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar fim de sessão");
            }
        }

        /// <summary>
        /// Atualiza sessão de trabalho
        /// </summary>
        public async Task SendWorkSessionUpdatedAsync(WorkSession session)
        {
            await SendWorkSessionUpdatedAsync(session, "Update");
        }

        /// <summary>
        /// Atualiza sessão de trabalho com motivo
        /// </summary>
        public async Task SendWorkSessionUpdatedAsync(WorkSession session, string updateReason)
        {
            try
            {
                var payload = new
                {
                    Session = session,
                    Reason = updateReason,
                    UpdatedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PutAsync($"api/sessions/{session.SessionId}", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar sessão");
            }
        }

        /// <summary>
        /// Envia heartbeat para manter conexão ativa
        /// </summary>
        public async Task SendHeartbeatAsync()
        {
            try
            {
                var heartbeat = new
                {
                    AgentId = Environment.MachineName,
                    Timestamp = DateTime.UtcNow,
                    Status = "Active"
                };

                var json = JsonSerializer.Serialize(heartbeat, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync("api/agents/heartbeat", content);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao enviar heartbeat");
            }
        }

        /// <summary>
        /// Método duplicado para compatibilidade (chama SendBomDataAsync)
        /// </summary>
        public async Task SendBOMDataAsync(BOMDataWithContext bomData)
        {
            await SendBomDataAsync(bomData);
        }

        /// <summary>
        /// Envia status da máquina
        /// </summary>
        public async Task SendMachineStatusAsync(object machineStatus)
        {
            try
            {
                var json = JsonSerializer.Serialize(machineStatus, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync("api/machines/status", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar status da máquina");
            }
        }

        /// <summary>
        /// Envia dados de peça
        /// </summary>
        public async Task SendPartDataAsync(object partData)
        {
            try
            {
                var json = JsonSerializer.Serialize(partData, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync("api/parts", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar dados de peça");
            }
        }

        /// <summary>
        /// Envia evento de documento
        /// </summary>
        public async Task SendDocumentActivityAsync(DocumentEvent documentEvent)
        {
            try
            {
                var activity = new ActivityData
                {
                    Type = documentEvent.EventType,
                    UserName = documentEvent.UserName,
                    FileName = Path.GetFileName(documentEvent.DocumentPath),
                    FilePath = documentEvent.DocumentPath,
                    Timestamp = documentEvent.Timestamp,
                    Metadata = documentEvent.Properties
                };

                await SendActivityAsync(activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar atividade de documento");
            }
        }
    }

    /// <summary>
    /// Dados de atividade para o feed
    /// </summary>
    public class ActivityData
    {
        public string Type { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

        public ActivityData()
        {
            Timestamp = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Informações do Agent para registro
    /// </summary>
    public class AgentInfo
    {
        public string MachineName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? InventorVersion { get; set; }
        public DateTime StartedAt { get; set; }
    }

    /// <summary>
    /// DTO para submissão de BOM
    /// </summary>
    public class BomSubmissionDto
    {
        public int ProjectId { get; set; }
        public int MachineId { get; set; }
        public string AssemblyPath { get; set; } = string.Empty;
        public BOMDataWithContext BomData { get; set; } = new();
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Sessão de trabalho
    /// </summary>
    public class WorkSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public string UserName { get; set; } = Environment.UserName;
        public string ProjectName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }
        public TimeSpan Duration => EndedAt.HasValue ? EndedAt.Value - StartedAt : DateTime.UtcNow - StartedAt;
        public List<string> FilesModified { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Evento de documento
    /// </summary>
    public class DocumentEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string UserName { get; set; } = Environment.UserName;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}