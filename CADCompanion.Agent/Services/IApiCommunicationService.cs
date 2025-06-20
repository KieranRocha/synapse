using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using CADCompanion.Shared.Models;

namespace CADCompanion.Agent.Services
{
    /// <summary>
    /// Interface completa para comunicação com a API backend
    /// </summary>
    public interface IApiCommunicationService
    {
        // Métodos básicos
        Task<bool> TestConnectionAsync();
        Task<List<ProjectInfo>> GetActiveProjectsAsync();
        Task<bool> SendBomDataAsync(BOMDataWithContext bomData);
        Task SendActivityAsync(ActivityData activity);
        Task<bool> RegisterAgentAsync(AgentInfo agentInfo);

        // Métodos adicionais que estavam faltando
        Task<bool> SubmitBomAsync(BomSubmissionDto bomSubmission);
        Task SendWorkSessionEndedAsync(WorkSession session);
        Task SendWorkSessionUpdatedAsync(WorkSession session);
        Task SendWorkSessionUpdatedAsync(WorkSession session, string updateReason);
        Task SendHeartbeatAsync();
        Task SendBOMDataAsync(BOMDataWithContext bomData); // Duplicata, mas mantendo por compatibilidade
        Task SendMachineStatusAsync(object machineStatus);
        Task SendPartDataAsync(object partData);
        Task SendDocumentActivityAsync(DocumentEvent documentEvent);
    }

    // DTOs necessários
    public class BomSubmissionDto
    {
        public int ProjectId { get; set; }
        public int MachineId { get; set; }
        public string AssemblyPath { get; set; } = string.Empty;
        public BOMDataWithContext BomData { get; set; } = new();
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }

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

    public class DocumentEvent
    {
        public string EventType { get; set; } = string.Empty; // Open, Save, Close
        public string DocumentPath { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string UserName { get; set; } = Environment.UserName;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}