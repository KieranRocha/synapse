using System;
using System.Collections.Generic;

namespace CADCompanion.Shared.Models
{
    /// <summary>
    /// Informações detalhadas de um projeto
    /// </summary>
    public class ProjectInfo
    {
        public ProjectInfo()
        {
            Machines = new List<MachineInfo>();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Status = ProjectStatus.Active;
        }

        // Identificação
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ContractNumber { get; set; }
        public string? Description { get; set; }

        // Localização
        public string? FolderPath { get; set; }
        public string? ServerPath { get; set; }

        // Status e datas
        public ProjectStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Informações comerciais
        public string? Client { get; set; }
        public string? ResponsibleEngineer { get; set; }
        public decimal? BudgetValue { get; set; }
        public decimal? ActualCost { get; set; }

        // Progresso
        public int ProgressPercentage { get; set; }
        public int EstimatedHours { get; set; }
        public int ActualHours { get; set; }

        // Relacionamentos
        public List<MachineInfo> Machines { get; set; }
        public int MachineCount => Machines?.Count ?? 0;

        // Metadados
        public string? CreatedBy { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public int TotalBomVersions { get; set; }

        // Propriedades calculadas
        public bool IsActive => Status == ProjectStatus.Active;
        public bool IsOverdue => EndDate.HasValue && EndDate.Value < DateTime.UtcNow && Status != ProjectStatus.Completed;
        public decimal? BudgetVariance => BudgetValue.HasValue && ActualCost.HasValue
            ? BudgetValue.Value - ActualCost.Value
            : null;
    }

    /// <summary>
    /// Status possíveis de um projeto
    /// </summary>
    public enum ProjectStatus
    {
        Planning,
        Active,
        OnHold,
        Review,
        Completed,
        Cancelled
    }

    /// <summary>
    /// Informações de uma máquina do projeto
    /// </summary>
    public class MachineInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FolderPath { get; set; }
        public string? AssemblyFile { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastBomExtraction { get; set; }
        public int BomVersionCount { get; set; }
        public bool IsActive { get; set; }
    }
}