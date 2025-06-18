// CADCompanion.Server/Program.cs - COMPLETO E CORRIGIDO
using CADCompanion.Server.Data;
using CADCompanion.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ SERVIÇOS ESSENCIAIS
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// ✅ CORS CORRIGIDO
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // Vite
                "http://localhost:3000",  // React
                "http://localhost:8080"   // Electron
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ✅ DATABASE
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ CACHE PARA DASHBOARD
builder.Services.AddMemoryCache();

// ✅ REGISTRAR TODOS OS SERVIÇOS NECESSÁRIOS
builder.Services.AddScoped<BomVersioningService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();

var app = builder.Build();

// ✅ PIPELINE CORRETO
app.UseCors("AllowFrontend"); // DEVE VIR PRIMEIRO

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();