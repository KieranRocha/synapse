// ADICIONE ao seu DbContext existente
public class MachineStatus
{
    public int Id { get; set; }
    public string MachineId { get; set; }
    public string Status { get; set; } // TRABALHANDO, ABERTA, FECHADA
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string? ProjectId { get; set; }
    public string UserName { get; set; }
    public string MachineName { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsActive { get; set; } = true;
}

// ADICIONE ao seu AppDbContext
public class AppDbContext : DbContext
{
    // Suas tabelas existentes...
    public DbSet<BOMVersion> BOMVersions { get; set; }
    public DbSet<Project> Projects { get; set; }

    // ✅ NOVA TABELA
    public DbSet<MachineStatus> MachineStatuses { get; set; }
}