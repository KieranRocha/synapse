// Em CADCompanion.Server/Program.cs

// 1. TODAS as cláusulas "using" devem vir primeiro.
using CADCompanion.Server.Data;
using CADCompanion.Server.Services;
using Microsoft.EntityFrameworkCore;

// 2. DEPOIS, vem todo o código de configuração e execução.
var builder = WebApplication.CreateBuilder(args);

// Adicionar serviços ao contêiner de injeção de dependência.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<BomVersioningService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configurar o pipeline de requisições HTTP.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();