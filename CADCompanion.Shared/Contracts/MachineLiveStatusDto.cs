// CADCompanion.Shared/Contracts/MachineLiveStatusDto.cs - CRIAR ARQUIVO

using System;

namespace CADCompanion.Shared.Contracts
{
    public class MachineLiveStatusDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastBomExtraction { get; set; }
        public int TotalBomVersions { get; set; }
        public string? CurrentFile { get; set; }
        public DateTime? LastActivity { get; set; }
        public DateTime UpdatedAt { get; set; }
        public MachineQuickStatsDto QuickStats { get; set; } = new();
    }

    public class MachineQuickStatsDto
    {
        public int BomVersionsThisWeek { get; set; }
        public DateTime? LastSaveTime { get; set; }
        public int ActiveUsersCount { get; set; }
    }
}