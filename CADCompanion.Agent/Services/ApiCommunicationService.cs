// CADCompanion.Agent/Services/ApiCommunicationService.cs (Corrigido)
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
using CADCompanion.Agent.Models;
using CADCompanion.Shared.Models;
using CADCompanion.Shared.Contracts;

namespace CADCompanion.Agent.Services
{
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

            var apiUrl = _configuration["ServerBaseUrl"] ?? "http://localhost:5047";
            _httpClient.BaseAddress = new Uri(apiUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CADCompanion.Agent/1.0");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} após {timespan} segundos para {context.GetHttpRequestMessage()?.RequestUri}");
                    });
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.GetAsync("health"));
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao testar conexão com API");
                return false;
            }
        }

        public async Task<List<ProjectInfo>> GetActiveProjectsAsync()
        {
            try
            {
                var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.GetAsync("api/projects/active"));
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<ProjectInfo>>(json, _jsonOptions) ?? new List<ProjectInfo>();
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

        public async Task SendActivityAsync(ActivityData activity)
        {
            try
            {
                var json = JsonSerializer.Serialize(activity, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/activity/log", content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Erro ao enviar atividade: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar atividade");
            }
        }

        public async Task<bool> RegisterAgentAsync(AgentInfo agentInfo)
        {
            try
            {
                var json = JsonSerializer.Serialize(agentInfo, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.PostAsync("api/agents/register", content));
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

        public async Task<bool> SubmitBomAsync(BomSubmissionDto bomSubmission)
        {
            try
            {
                var json = JsonSerializer.Serialize(bomSubmission, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.PostAsync("api/boms/submit", content));

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"BOM de {bomSubmission.AssemblyFilePath} enviado com sucesso.");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Falha ao enviar BOM para {bomSubmission.AssemblyFilePath}. Status: {response.StatusCode}, Resposta: {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exceção ao enviar BOM para {bomSubmission.AssemblyFilePath}");
                return false;
            }
        }

        public async Task SendWorkSessionEndedAsync(WorkSession session)
        {
            try
            {
                var json = JsonSerializer.Serialize(session, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync("api/sessions/end", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar fim de sessão");
            }
        }

        public async Task SendWorkSessionUpdatedAsync(WorkSession session, string updateReason)
        {
            try
            {
                var payload = new { Session = session, Reason = updateReason, UpdatedAt = DateTime.UtcNow };
                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PutAsync($"api/sessions/{session.Id}", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar sessão");
            }
        }

        public async Task SendHeartbeatAsync()
        {
            try
            {
                await _httpClient.PostAsync("api/session/heartbeat", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao enviar heartbeat");
            }
        }

        public async Task SendMachineStatusAsync(object machineStatus)
        {
            try
            {
                var json = JsonSerializer.Serialize(machineStatus, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync("api/machine-status", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar status da máquina");
            }
        }

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

        public async Task SendDocumentActivityAsync(Models.DocumentEvent documentEvent)
        {
            try
            {
                var json = JsonSerializer.Serialize(documentEvent, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync("api/activity/document", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar atividade de documento");
            }
        }
    }
}