using System.Text;
using AutomotoraSaaS.Api.Filters;
using AutomotoraSaaS.Api.MultiTenancy;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Infrastructure;
using AutomotoraSaaS.Infrastructure.Auth;
using AutomotoraSaaS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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

// La configuración de JWT se lee y se valida acá, en el arranque. Una API que levanta y
// empieza a firmar tokens con una clave vacía es peor que una que no levanta.
var jwt = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();
jwt.Validar();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sin el mapeo de claims heredado: los claims quedan con el nombre con el que se
        // emitieron ("sub", "role", "tenant_id") en vez de convertirse en las URIs largas
        // de WS-Federation. Lo que se firma es lo que se lee.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,

            // Treinta segundos, no los cinco minutos que trae por defecto: el access token
            // dura quince minutos, y una tolerancia de cinco sería un tercio de su vida.
            ClockSkew = TimeSpan.FromSeconds(30),

            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimsDeLaApp.Rol,
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.SoloSuperAdmin, p => p.RequireRole(Roles.SuperAdmin))
    .AddPolicy(Politicas.SoloOwner, p => p.RequireRole(Roles.Owner))
    .AddPolicy(Politicas.PanelDeTenant, p => p.RequireRole(Roles.Owner, Roles.Seller));

builder.Services.AddSingleton(TimeProvider.System);

// Errores en formato ProblemDetails, uno solo para toda la API: los que devuelven los
// controllers, los que genera el binding y los que salen de una excepción no manejada.
builder.Services.AddProblemDetails();

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

builder.Services.AddControllers(options => options.Filters.Add<ValidacionFluentFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Description = "Pegá acá el accessToken que devuelve POST /api/auth/login.",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = JwtBearerDefaults.AuthenticationScheme,
            },
        }] = Array.Empty<string>(),
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Bloqueante a propósito: mantiene el Main sincrónico, y lo único que se demora es
    // el arranque de desarrollo mientras se siembra una vez.
    SembrarDesarrolloAsync(app).GetAwaiter().GetResult();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();

// Entre la autenticación y la autorización: necesita el token ya validado para leer el
// claim del tenant, y todo lo que corre después ya trabaja con el tenant puesto.
app.UseResolucionDeTenant();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Siembra las automotoras y el catálogo de desarrollo. Solo corre en Development y solo
// si hay una contraseña en Seed:Password: nunca inventa una por omisión.
static async Task SembrarDesarrolloAsync(WebApplication app)
{
    var password = app.Configuration["Seed:Password"];

    if (string.IsNullOrWhiteSpace(password))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        if (!await db.Database.CanConnectAsync().ConfigureAwait(false))
        {
            logger.LogWarning(
                "No se pudo conectar a la base, se omite el seed. Revisá ConnectionStrings:Default " +
                "y corré las migraciones con 'dotnet dotnet-ef database update'.");
            return;
        }

        await SeedDeDesarrollo
            .EjecutarAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), password)
            .ConfigureAwait(false);

        logger.LogInformation("Seed de desarrollo aplicado.");
    }
    catch (Exception ex)
    {
        // Que el seed falle no puede impedir que la API levante: en desarrollo se arranca
        // muchas veces con la base a medio migrar.
        logger.LogError(ex, "Falló el seed de desarrollo. La API arranca igual.");
    }
}

/// <summary>
/// Expuesta como parcial pública para que <c>WebApplicationFactory</c> pueda levantar
/// la API dentro de los tests de integración.
/// </summary>
public partial class Program;
