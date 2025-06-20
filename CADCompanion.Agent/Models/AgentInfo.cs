// CADCompanion.Agent/Models/AgentInfo.cs (Novo Arquivo)
using System;

namespace CADCompanion.Agent.Models
{
    public class AgentInfo
    {
        public string MachineName { get; set; } = Environment.MachineName;
        public string UserName { get; set; } = Environment.UserName;
        public string Version { get; set; } = "1.0.0"; // Pode ser pego da assembly
        public string? InventorVersion { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    }
}