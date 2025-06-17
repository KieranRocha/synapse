// CADCompanion.Server/Data/AppDbContext.cs - ATUALIZADO
using CADCompanion.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BomVersion> BomVersions { get; set; }
    public DbSet<Project> Projects { get; set; }

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

        // Configuração do relacionamento Project -> BomVersions
        modelBuilder.Entity<BomVersion>(entity =>
        {
            // Relacionamento com Project (se você quiser implementar depois)
            // entity.HasOne<Project>()
            //     .WithMany(p => p.BomVersions)
            //     .HasForeignKey(b => b.ProjectId)
            //     .OnDelete(DeleteBehavior.Cascade);
        });
    }
}