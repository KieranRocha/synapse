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

// Configura o DbContext para usar PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registra seus serviços customizados
builder.Services.AddScoped<BomVersioningService>();

// --- Fim da Configuração dos Serviços ---


var app = builder.Build();

// --- Início da Configuração do Pipeline HTTP ---

// Habilita o Swagger em ambiente de desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ✅ LINHA CRÍTICA 2: Mapeia as rotas para os controllers.
app.MapControllers();

// --- Fim da Configuração do Pipeline HTTP ---

app.Run();