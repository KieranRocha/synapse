// CADCompanion.Shared/Models/ProjectInfo.cs (Corrigido e Expandido)
using System;
using System.Collections.Generic;

namespace CADCompanion.Shared.Models
{
    public class ProjectInfo
    {
        public int Id { get; set; }
        public int ProjectId => Id; // Adicionado para compatibilidade
        public required string Number { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? Status { get; set; }

        // Campos adicionados que eram esperados pelo Agent
        public string? DetectedName { get; set; }
        public string? FolderPath { get; set; }
        public bool IsValidProject { get; set; } = false;
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }
}