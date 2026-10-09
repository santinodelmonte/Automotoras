using System.Text;
using System.Threading.RateLimiting;
using AutomotoraSaaS.Api.Configuracion;
using AutomotoraSaaS.Api.Controllers;
using AutomotoraSaaS.Api.Filters;
using AutomotoraSaaS.Api.MultiTenancy;
using AutomotoraSaaS.Api.Seguridad;
using AutomotoraSaaS.Api.Sitio;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Infrastructure;
using AutomotoraSaaS.Infrastructure.Auth;
using AutomotoraSaaS.Infrastructure.Persistence;
using FluentValidation;
using AutomotoraSaaS.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Toda la configuración (connection string, JWT, storage, secreto de jobs) se lee de
// appsettings + variables de entorno. Nada hardcodeado: en SmarterASP.NET los valores
// reales llegan por variables de entorno / appsettings.Production.json fuera del repo.
builder.Configuration.AddEnvironmentVariables();

// Los errores no manejados y los logs de nivel Error van a Sentry, para enterarse de una
// falla en producción antes de que avise el cliente. Sin Sentry:Dsn queda apagado, que es
// lo que pasa en desarrollo y en los tests. Sin datos personales: ni IPs, ni usuarios, ni
// cuerpos de request, por la misma regla que la analítica del sitio.
builder.WebHost.UseSentry(opciones =>
{
    opciones.SendDefaultPii = false;
    opciones.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
    opciones.Environment = builder.Environment.EnvironmentName;
});

// Sin "Server: Kestrel": no le dice nada útil a nadie salvo a quien busca qué atacar.
builder.WebHost.ConfigureKestrel(opciones => opciones.AddServerHeader = false);

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

// El endpoint de eventos no tiene autenticación y escribe en la tabla que más crece del
// sistema. Sin tope, un script la llena de eventos falsos y deja sin valor los reportes de
// todas las automotoras a la vez.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(LimitesDeEventos.Politica, contexto => RateLimitPartition.GetFixedWindowLimiter(
        // Por IP. No por sesión: el id de sesión lo elige el cliente, así que limitar por
        // ahí es limitar a quien se porta bien.
        partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = LimitesDeEventos.EventosPorVentana,
            Window = LimitesDeEventos.Ventana,

            // Sin cola: al que se pasa se le dice que no y sigue navegando. Encolar
            // requests de métricas solo sostiene conexiones abiertas para nada.
            QueueLimit = 0,
        }));

    options.AddPolicy(LimitesDeErroresDelCliente.Politica, contexto => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = LimitesDeErroresDelCliente.ErroresPorVentana,
            Window = LimitesDeErroresDelCliente.Ventana,
            QueueLimit = 0,
        }));

    var loginsPorMinuto = builder.Configuration.GetValue(
        "Seguridad:LoginsPorMinutoPorIp", LimitesDeLogin.LoginsPorMinutoPorDefecto);

    options.AddPolicy(LimitesDeLogin.Politica, contexto => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginsPorMinuto,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    options.OnRejected = (contexto, _) =>
    {
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            contexto.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(espera.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };
});

// Comprimido, el bundle del frontend pesa un tercio: es lo que más le cambia la primera
// carga a quien abre el link de un auto desde WhatsApp con 4G. Solo texto que no lleva
// secretos —páginas, scripts, estilos, sitemap—; las respuestas JSON de la API no, porque
// llevan tokens y comprimirlas sobre HTTPS las expone a BREACH.
builder.Services.AddResponseCompression(opciones =>
{
    opciones.EnableForHttps = true;
    opciones.Providers.Add<BrotliCompressionProvider>();
    opciones.Providers.Add<GzipCompressionProvider>();
    opciones.MimeTypes =
    [
        "text/html",
        "text/css",
        "text/javascript",
        "application/javascript",
        "text/plain",
        "application/xml",
        "text/xml",
        "image/svg+xml",
    ];
});

// El nivel por defecto es el más rápido, y con ese el bundle salía más pesado en Brotli
// que en gzip. Optimal sigue siendo barato para archivos de este tamaño.
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Optimal);

// Errores en formato ProblemDetails, uno solo para toda la API: los que devuelven los
// controllers, los que genera el binding y los que salen de una excepción no manejada.
builder.Services.AddProblemDetails();

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidacionFluentFilter>();
    options.Filters.Add<PasswordProvisoriaFilter>();
});
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
builder.Services.AddSingleton<PlantillaDelSitio>();

if (DependencyInjection.EsSqlite(builder.Configuration) && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Database:Proveedor=Sqlite es solo para la base descartable de los tests de punta a punta, en Development.");
}

var app = builder.Build();

if (app.Environment.IsProduction())
{
    VerificacionDeProduccion.Aplicar(app.Configuration, app.Logger);
}

app.UseExceptionHandler();

// Antes que todo lo que puede responder —archivos, fallback del sitio, la API— para que
// ninguna respuesta salga sin ellos.
app.UseHeadersDeSeguridad(app.Configuration);

app.UseResponseCompression();

// HSTS solo viaja en respuestas HTTPS: un dominio propio que todavía no tiene certificado
// sigue andando por HTTP. Sin includeSubDomains por lo mismo.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // La base descartable de los tests de punta a punta no tiene migraciones: se crea del
    // modelo, como en los tests de integración, y después se siembra como cualquier otra.
    if (DependencyInjection.EsSqlite(app.Configuration))
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    // Bloqueante a propósito: mantiene el Main sincrónico, y lo único que se demora es
    // el arranque de desarrollo mientras se siembra una vez.
    SembrarDesarrolloAsync(app).GetAwaiter().GetResult();
}

CrearPrimerSuperAdminAsync(app).GetAwaiter().GetResult();

ServirImagenesLocales(app);

// El frontend compilado, publicado en wwwroot. Los archivos de /assets llevan el hash en
// el nombre, así que se pueden cachear para siempre: un deploy nuevo cambia el nombre.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = archivo =>
    {
        if (archivo.Context.Request.Path.StartsWithSegments("/assets", StringComparison.Ordinal))
        {
            archivo.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    },
});

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();

// Va después de la autenticación —necesita el token ya validado para leer el claim del
// tenant— y antes del routing, porque saca el prefijo /t/{slug} de la ruta. Si el routing
// corriera primero intentaría matchear "/t/norte/api/public/vehiculos", que no es la ruta
// que declara ningún controller, y el sitio público entero daría 404.
app.UseResolucionDeTenant();

// Explícito y acá. Si no se llama, WebApplication lo inserta al principio del pipeline,
// o sea antes de la reescritura de la ruta.
app.UseRouting();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

// Lo que no es API ni archivo es una página del frontend: sale el index.html con las
// etiquetas de la automotora o del vehículo ya puestas (ver SitioDelFrontend).
app.MapGet("/robots.txt", SitioDelFrontend.RobotsAsync);
app.MapFallback(SitioDelFrontend.ServirAsync);

app.Run();

// Con el proveedor local, las fotos viven en una carpeta fuera del repo y las sirve la
// propia API. Es solo para desarrollo: en producción el proveedor es R2 y esto no corre,
// porque en shared hosting IIS el disco del servidor no es un lugar donde guardar nada.
static void ServirImagenesLocales(WebApplication app)
{
    var opciones = app.Configuration.GetSection(StorageOptions.Seccion).Get<StorageOptions>();

    if (opciones is null || !opciones.EsLocal || string.IsNullOrWhiteSpace(opciones.LocalRootPath))
    {
        return;
    }

    var raiz = Path.GetFullPath(opciones.LocalRootPath);
    Directory.CreateDirectory(raiz);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(raiz),
        RequestPath = "/uploads",

        // Sin ServeUnknownFileTypes: solo salen los tipos conocidos. Lo que se guarda ya
        // pasó por la validación de firma, y esto es el segundo cerrojo.
    });
}

// El primer SuperAdmin de una base nueva, desde PrimerSuperAdmin:* (ver PrimerSuperAdmin).
// Si falla —la base no responde, la contraseña no alcanza— la API arranca igual: el sitio
// público no depende de esto, y el motivo queda en el log y en Sentry.
static async Task CrearPrimerSuperAdminAsync(WebApplication app)
{
    var seccion = app.Configuration.GetSection(PrimerSuperAdmin.Seccion);
    var email = seccion["Email"];
    var password = seccion["Password"];

    if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(PrimerSuperAdmin));

    try
    {
        var resultado = await PrimerSuperAdmin
            .AsegurarAsync(
                scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                email,
                password)
            .ConfigureAwait(false);

        if (resultado == PrimerSuperAdmin.Resultado.Creado)
        {
            logger.LogWarning(
                "SuperAdmin {Email} creado con contraseña provisoria. Sacá las variables PrimerSuperAdmin__* del servidor.",
                email);
        }
        else
        {
            logger.LogWarning("Ya hay un SuperAdmin: PrimerSuperAdmin__* se ignora. Sacá esas variables del servidor.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "No se pudo crear el primer SuperAdmin. La API arranca igual.");
    }
}

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
            .EjecutarAsync(
                db,
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                scope.ServiceProvider.GetRequiredService<TimeProvider>(),
                password)
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
