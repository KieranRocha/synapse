// Em CADCompanion.Server/Data/AppDbContext.cs
using CADCompanion.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CADCompanion.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BomVersion> BomVersions { get; set; }
    // public DbSet<Project> Projects { get; set; }
    // public DbSet<Machine> Machines { get; set; }
}