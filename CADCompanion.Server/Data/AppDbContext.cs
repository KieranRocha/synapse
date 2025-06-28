// CADCompanion.Server/Data/AppDbContext.cs - ATUALIZADO
using CADCompanion.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ✅ DbSets existentes
    public DbSet<Project> Projects { get; set; }
    public DbSet<Machine> Machines { get; set; }
    public DbSet<BomVersion> BomVersions { get; set; }
    
    // ✅ NOVOS: Catálogo de Peças
    public DbSet<Part> Parts { get; set; }
    public DbSet<PartNumberSequence> PartNumberSequences { get; set; }
    public DbSet<BomPartUsage> BomPartUsages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ CONFIGURAÇÕES EXISTENTES (mantidas)
        ConfigureProject(modelBuilder);
        ConfigureMachine(modelBuilder);
        ConfigureBomVersion(modelBuilder);
        
        // ✅ NOVAS CONFIGURAÇÕES
        ConfigurePart(modelBuilder);
        ConfigurePartNumberSequence(modelBuilder);
        ConfigureBomPartUsage(modelBuilder);
    }

    private void ConfigureProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ContractNumber).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.FolderPath).HasMaxLength(500);
            entity.Property(e => e.Client).HasMaxLength(100);
            entity.Property(e => e.ResponsibleEngineer).HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.BudgetValue).HasPrecision(18, 2);
            entity.Property(e => e.ActualCost).HasPrecision(18, 2);
            entity.Property(e => e.ProgressPercentage).HasPrecision(5, 2);

            // Índices
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.ContractNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }

    private void ConfigureMachine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.OperationNumber).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.FolderPath).HasMaxLength(500);
            entity.Property(e => e.MainAssemblyPath).HasMaxLength(200);
            entity.Property(e => e.Status).HasConversion<string>();

            // Relacionamento com Project
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Machines)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índices
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.OperationNumber);
            entity.HasIndex(e => e.CreatedAt);
        });
    }

    private void ConfigureBomVersion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BomVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProjectId).IsRequired();
            entity.Property(e => e.MachineId).IsRequired();
            entity.Property(e => e.AssemblyFilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ExtractedBy).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Items).HasColumnType("jsonb").IsRequired();

            // Índices
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.MachineId);
            entity.HasIndex(e => e.ExtractedAt);
            entity.HasIndex(e => new { e.ProjectId, e.MachineId, e.VersionNumber }).IsUnique();
        });
    }

    // ✅ NOVA: Configuração da tabela Parts
    private void ConfigurePart(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Part>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Part Number único e obrigatório
            entity.Property(e => e.PartNumber)
                .IsRequired()
                .HasMaxLength(6)
                .IsFixedLength(); // Sempre 6 dígitos
            
            entity.HasIndex(e => e.PartNumber).IsUnique();
            
            // Outros campos
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Material).HasMaxLength(100);
            entity.Property(e => e.Supplier).HasMaxLength(100);
            entity.Property(e => e.Manufacturer).HasMaxLength(100);
            entity.Property(e => e.ManufacturerPartNumber).HasMaxLength(100);
            
            // Campos numéricos com precisão
            entity.Property(e => e.Weight).HasPrecision(10, 3); // kg
            entity.Property(e => e.Cost).HasPrecision(10, 2);   // R$
            
            // JSON para propriedades customizadas
            entity.Property(e => e.CustomProperties).HasColumnType("jsonb");
            
            // Enum como string
            entity.Property(e => e.Status).HasConversion<string>();
            
            // Índices para performance
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Supplier);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsStandardPart);
            
            // Índice de texto para busca por descrição
            entity.HasIndex(e => e.Description);
        });
    }

    // ✅ NOVA: Configuração da sequência de part numbers
    private void ConfigurePartNumberSequence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PartNumberSequence>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.SequenceType)
                .IsRequired()
                .HasMaxLength(50);
            
            // Garantir que existe apenas uma sequência por tipo
            entity.HasIndex(e => e.SequenceType).IsUnique();
        });
    }

    // ✅ NOVA: Configuração da tabela de uso das peças
    private void ConfigureBomPartUsage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BomPartUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Relacionamento com BomVersion
            entity.HasOne(e => e.BomVersion)
                .WithMany() // BomVersion não precisa navegar de volta
                .HasForeignKey(e => e.BomVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Relacionamento com Part (por PartNumber)
            entity.HasOne(e => e.Part)
                .WithMany(p => p.BomUsages)
                .HasForeignKey(e => e.PartNumber)
                .HasPrincipalKey(p => p.PartNumber)
                .OnDelete(DeleteBehavior.Restrict); // Não deletar Part se estiver em uso
            
            entity.Property(e => e.PartNumber).IsRequired().HasMaxLength(6);
            entity.Property(e => e.ParentPartNumber).HasMaxLength(6);
            entity.Property(e => e.ReferenceDesignator).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(200);
            
            // Precisão para quantidade
            entity.Property(e => e.Quantity).HasPrecision(10, 3);
            
            // Índices para where-used queries
            entity.HasIndex(e => e.PartNumber); // "Onde a peça X é usada?"
            entity.HasIndex(e => e.BomVersionId); // "Que peças tem no BOM Y?"
            entity.HasIndex(e => e.ParentPartNumber); // Hierarquia
            entity.HasIndex(e => new { e.BomVersionId, e.PartNumber }).IsUnique(); // Evitar duplicatas
        });
    }
}