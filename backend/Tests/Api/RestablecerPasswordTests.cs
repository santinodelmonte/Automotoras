using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Users;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// El camino para un dueño que se olvidó la contraseña: el SuperAdmin le pone una
/// provisoria, y con ella solo puede cambiarla.
/// </summary>
public sealed class RestablecerPasswordTests : IClassFixture<FabricaDeApi>
{
    private const string Provisoria = "Provisoria-segura-9";

    private readonly FabricaDeApi _api;

    public RestablecerPasswordTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_superadmin_pone_una_provisoria_y_la_vieja_deja_de_servir()
    {
        const string email = "olvidadizo@norte.uy";
        _api.AgregarVendedorDeNorte(email);
        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await admin.PostAsJsonAsync(
            $"/api/admin/tenants/{_api.TenantNorte}/restablecer-password",
            new RestablecerPasswordRequest(email, Provisoria));

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        using var cliente = _api.CreateClient();
        var vieja = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, FabricaDeApi.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, vieja.StatusCode);

        var sesion = await _api.LoginAsync(email, Provisoria);
        Assert.True(sesion.Usuario.DebeCambiarPassword);
    }

    /// <summary>Con el id de Sur no se le toca la contraseña a un usuario de Norte.</summary>
    [Fact]
    public async Task Solo_encuentra_usuarios_de_la_automotora_indicada()
    {
        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await admin.PostAsJsonAsync(
            $"/api/admin/tenants/{_api.TenantSur}/restablecer-password",
            new RestablecerPasswordRequest(FabricaDeApi.EmailOwnerNorte, Provisoria));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_owner_no_puede_usarlo()
    {
        using var owner = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await owner.PostAsJsonAsync(
            $"/api/admin/tenants/{_api.TenantNorte}/restablecer-password",
            new RestablecerPasswordRequest(FabricaDeApi.EmailVendedorNorte, Provisoria));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
