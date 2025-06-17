using CADCompanion.Server.Data;
using CADCompanion.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Início da Configuração dos Serviços ---

// Adiciona os serviços essenciais para a API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ LINHA CRÍTICA 1: Garante que os controllers sejam registrados.
builder.Services.AddControllers();

// ✅ CONFIGURAÇÃO CORS ADICIONADA
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // Vite dev server
                "http://localhost:3000",  // React dev server alternativo
                "http://localhost:8080"   // Electron dev
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed(_ => true) // Para desenvolvimento
            .AllowCredentials();
    });
});

// Configura o DbContext para usar PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registra seus serviços customizados
builder.Services.AddScoped<BomVersioningService>();
builder.Services.AddScoped<IProjectService, ProjectService>();

// --- Fim da Configuração dos Serviços ---


var app = builder.Build();

// --- Início da Configuração do Pipeline HTTP ---

// ✅ USAR CORS - DEVE VIR ANTES DE OUTROS MIDDLEWARES
app.UseCors("AllowFrontend");

// Habilita o Swagger em ambiente de desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ✅ REMOVER HTTPS REDIRECT POR ENQUANTO (está causando warning)
// app.UseHttpsRedirection();

// ✅ LINHA CRÍTICA 2: Mapeia as rotas para os controllers.
app.MapControllers();

// --- Fim da Configuração do Pipeline HTTP ---

app.Run();