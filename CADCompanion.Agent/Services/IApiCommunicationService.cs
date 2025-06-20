// CADCompanion.Agent/Services/IApiCommunicationService.cs (Corrigido)
using CADCompanion.Agent.Models;
using CADCompanion.Shared.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;
using CADCompanion.Shared.Models; // Adicionado para encontrar ProjectInfo
namespace CADCompanion.Agent.Services
{
    /// <summary>
    /// Interface consolidada para comunicação com a API backend
    /// </summary>
    public interface IApiCommunicationService
    {
        Task<bool> TestConnectionAsync();
        Task<List<ProjectInfo>> GetActiveProjectsAsync();
        Task<bool> SubmitBomAsync(BomSubmissionDto bomSubmission);
        Task SendActivityAsync(ActivityData activity);
        Task<bool> RegisterAgentAsync(AgentInfo agentInfo);
        Task SendWorkSessionEndedAsync(WorkSession session);
        Task SendWorkSessionUpdatedAsync(WorkSession session, string updateReason);
        Task SendHeartbeatAsync();
        Task SendMachineStatusAsync(object machineStatus);
        Task SendPartDataAsync(object partData);
        Task SendDocumentActivityAsync(Models.DocumentEvent documentEvent);
    }
}