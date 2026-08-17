using AutomotoraSaaS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Toda la configuración (connection string, JWT, storage, secreto de jobs) se lee de
// appsettings + variables de entorno. Nada hardcodeado: en SmarterASP.NET los valores
// reales llegan por variables de entorno / appsettings.Production.json fuera del repo.
builder.Configuration.AddEnvironmentVariables();

const string FrontendCorsPolicy = "FrontendCors";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCorsPolicy);
app.MapControllers();

app.Run();

/// <summary>
/// Expuesta como parcial pública para que <c>WebApplicationFactory</c> pueda levantar
/// la API dentro de los tests de integración.
/// </summary>
public partial class Program;
