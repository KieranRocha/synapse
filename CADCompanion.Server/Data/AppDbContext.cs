// CADCompanion.Server/Data/AppDbContext.cs - CORRIGIDO
using CADCompanion.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ✅ ADICIONADO: DbSet que estava faltando
    public DbSet<Project> Projects { get; set; }
    public DbSet<Machine> Machines { get; set; }
    public DbSet<BomVersion> BomVersions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração da entidade Project
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ContractNumber)
                .HasMaxLength(50);

            entity.Property(e => e.Description)
                .HasMaxLength(200);

            entity.Property(e => e.FolderPath)
                .HasMaxLength(500);

            entity.Property(e => e.Client)
                .HasMaxLength(100);

            entity.Property(e => e.ResponsibleEngineer)
                .HasMaxLength(50);

            // Conversão do enum para string no banco
            entity.Property(e => e.Status)
                .HasConversion<string>();

            // Precisão para campos monetários
            entity.Property(e => e.BudgetValue)
                .HasPrecision(18, 2);

            entity.Property(e => e.ActualCost)
                .HasPrecision(18, 2);

            entity.Property(e => e.ProgressPercentage)
                .HasPrecision(5, 2);

            // Índices para performance
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.ContractNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // ✅ CONFIGURAÇÃO MACHINE CORRIGIDA
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.OperationNumber)
                .HasMaxLength(50);

            entity.Property(e => e.Description)
                .HasMaxLength(200);

            entity.Property(e => e.FolderPath)
                .HasMaxLength(500);

            entity.Property(e => e.MainAssemblyPath)
                .HasMaxLength(200);

            // Conversão do enum para string
            entity.Property(e => e.Status)
                .HasConversion<string>();

            // ✅ RELACIONAMENTO COM PROJECT
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Machines)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índices para performance
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.OperationNumber);
            entity.HasIndex(e => e.CreatedAt);
        });
        modelBuilder.Entity<BomVersion>(entity =>
        {
            entity.HasKey(e => e.Id);

            // ✅ CORRIGIDO: ProjectId como int (FK)
            entity.Property(e => e.ProjectId)
                .IsRequired();

            // ✅ CORRIGIDO: MachineId como int (FK)
            entity.Property(e => e.MachineId)
                .IsRequired();

            entity.Property(e => e.AssemblyFilePath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.ExtractedBy)
                .IsRequired()
                .HasMaxLength(100);

            // Campo JSONB para PostgreSQL
            entity.Property(e => e.Items)
                .HasColumnType("jsonb")
                .IsRequired();

            // ✅ ADICIONADO: Relacionamentos FK


            // Índices para queries frequentes
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.MachineId);
            entity.HasIndex(e => e.ExtractedAt);
            entity.HasIndex(e => new { e.ProjectId, e.MachineId, e.VersionNumber })
                .IsUnique();
        });
    }
}