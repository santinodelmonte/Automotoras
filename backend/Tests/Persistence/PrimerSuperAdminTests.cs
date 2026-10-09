using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Infrastructure.Auth;
using AutomotoraSaaS.Infrastructure.MultiTenancy;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Tests.Persistence;

/// <summary>
/// El primer SuperAdmin se crea una sola vez. Lo que importa probar es lo que pasa cuando
/// las variables se quedan puestas en el servidor: no pueden volver a crear ni a pisar nada.
/// </summary>
public sealed class PrimerSuperAdminTests : IDisposable
{
    private const string Password = "Primera-clave-1";

    private readonly SqliteConnection _conexion;
    private readonly AppDbContext _db;
    private readonly PasswordHasherPbkdf2 _hasher = new();

    public PrimerSuperAdminTests()
    {
        _conexion = new SqliteConnection("Filename=:memory:");
        _conexion.Open();

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexion)
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new AppDbContext(opciones, new TenantContext(), TimeProvider.System);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task En_una_base_vacia_lo_crea_con_contrasena_provisoria()
    {
        var resultado = await PrimerSuperAdmin.AsegurarAsync(_db, _hasher, " Admin@Ejemplo.UY ", Password);

        Assert.Equal(PrimerSuperAdmin.Resultado.Creado, resultado);

        var usuario = await _db.Users.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("admin@ejemplo.uy", usuario.Email);
        Assert.Equal(RolUsuario.SuperAdmin, usuario.Rol);
        Assert.Null(usuario.TenantId);
        Assert.True(usuario.DebeCambiarPassword);
        Assert.True(_hasher.Verificar(Password, usuario.PasswordHash));
    }

    [Fact]
    public async Task Con_un_superadmin_ya_creado_no_crea_otro_ni_toca_la_contrasena()
    {
        await PrimerSuperAdmin.AsegurarAsync(_db, _hasher, "admin@ejemplo.uy", Password);
        var hashOriginal = (await _db.Users.IgnoreQueryFilters().SingleAsync()).PasswordHash;

        var resultado = await PrimerSuperAdmin.AsegurarAsync(_db, _hasher, "otro@ejemplo.uy", "Otra-clave-22");

        Assert.Equal(PrimerSuperAdmin.Resultado.YaExistia, resultado);
        var usuario = await _db.Users.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(hashOriginal, usuario.PasswordHash);
    }

    [Fact]
    public async Task Sin_variables_no_hace_nada()
    {
        var resultado = await PrimerSuperAdmin.AsegurarAsync(_db, _hasher, null, "");

        Assert.Equal(PrimerSuperAdmin.Resultado.SinConfigurar, resultado);
        Assert.False(await _db.Users.IgnoreQueryFilters().AnyAsync());
    }

    [Theory]
    [InlineData("admin@ejemplo.uy", "corta1")]
    [InlineData("admin@ejemplo.uy", "solo-letras-sin-numeros")]
    [InlineData("no-es-un-email", Password)]
    [InlineData(null, Password)]
    public async Task Rechaza_una_contrasena_debil_o_un_email_invalido(string? email, string password)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PrimerSuperAdmin.AsegurarAsync(_db, _hasher, email, password));

        Assert.False(await _db.Users.IgnoreQueryFilters().AnyAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }
}
