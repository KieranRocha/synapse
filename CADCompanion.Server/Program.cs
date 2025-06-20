// CADCompanion.Server/Program.cs - TODOS OS SERVIÇOS REGISTRADOS
using CADCompanion.Server.Data;
using CADCompanion.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ CONFIGURAÇÃO DO BANCO DE DADOS
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ REGISTRAR MEMORYCACHE - NECESSÁRIO PARA DASHBOARDSERVICE
builder.Services.AddMemoryCache();

// ✅ REGISTRO DE TODOS OS SERVIÇOS - NA ORDEM CORRETA
// Serviços base primeiro
builder.Services.AddScoped<BomVersioningService>(); // ✅ FALTAVA ESTE!

// Serviços principais
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IMachineService, MachineService>();

// Serviços que dependem de outros
builder.Services.AddScoped<IDashboardService, DashboardService>(); // Precisa de BomVersioningService

// Outros serviços (se existirem)
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>(); // Descomente se existir

// ✅ CONFIGURAÇÃO DE CONTROLLERS
builder.Services.AddControllers();

// ✅ CONFIGURAÇÃO DE CORS PARA DESENVOLVIMENTO
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",  // React dev server
                "http://localhost:5173",  // Vite dev server  
                "http://localhost:8080"   // Electron (se usar)
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ✅ CONFIGURAÇÃO DE LOGGING
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// ✅ SWAGGER PARA DOCUMENTAÇÃO DA API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ✅ CONFIGURAÇÃO DO PIPELINE DE REQUEST
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// ✅ MIDDLEWARES - ORDEM CRÍTICA!
app.UseHttpsRedirection();
app.UseCors("AllowFrontend"); // ✅ ANTES do UseRouting!
app.UseRouting();
app.UseAuthorization();

// ✅ MAPPING DE CONTROLLERS
app.MapControllers();

// ✅ ENDPOINT DE HEALTH CHECK
app.MapGet("/health", () => "OK");

// ✅ EXECUTAR MIGRATIONS AUTOMATICAMENTE (DESENVOLVIMENTO)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        context.Database.Migrate();
        Console.WriteLine("✅ Database migrated successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error migrating database: {ex.Message}");
        // Não falhar a aplicação por causa de migration
    }
}

Console.WriteLine("🚀 CADCompanion Server started");
Console.WriteLine($"🌐 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"📱 API Base URL: http://localhost:5047");
Console.WriteLine($"📚 Swagger UI: http://localhost:5047/swagger");

app.Run();