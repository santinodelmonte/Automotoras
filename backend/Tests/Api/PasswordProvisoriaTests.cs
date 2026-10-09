using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutomotoraSaaS.Core.Admin;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Core.Users;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// La contraseña que pone otra persona sirve para entrar una vez y cambiarla. Nada más.
/// </summary>
public sealed class PasswordProvisoriaTests : IClassFixture<FabricaDeApi>
{
    private const string Provisoria = "Clave-provisoria-9";
    private const string Propia = "Clave-propia-del-duenio-7";

    private readonly FabricaDeApi _api;

    public PasswordProvisoriaTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_duenio_que_da_de_alta_el_superadmin_tiene_que_cambiar_la_contrasenia_antes_de_usar_el_panel()
    {
        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        var alta = await admin.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "provisoria", "Provisoria", null, "owner@provisoria.uy", "Owner", Provisoria, CodigosDePlan.Demanda));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var sesion = await _api.LoginAsync("owner@provisoria.uy", Provisoria);
        Assert.True(sesion.Usuario.DebeCambiarPassword);

        using var cliente = Cliente(sesion);

        // Con la provisoria no anda nada más que la sesión y el cambio.
        var bloqueado = await cliente.GetAsync("/api/vehiculos");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
        var problema = await bloqueado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("debe-cambiar-password", problema.GetProperty("type").GetString());

        var yo = await cliente.GetFromJsonAsync<UsuarioDto>("/api/auth/me");
        Assert.True(yo!.DebeCambiarPassword);

        // El sitio público no depende de quién mira: sigue andando.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/t/norte/api/public/tenant")).StatusCode);

        var cambio = await cliente.PostAsJsonAsync("/api/auth/password", new CambiarPasswordPropiaRequest(Provisoria, Propia));
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);

        var nueva = await cambio.Content.ReadFromJsonAsync<SesionDto>();
        Assert.False(nueva!.Usuario.DebeCambiarPassword);

        using var liberado = Cliente(nueva);
        Assert.Equal(HttpStatusCode.OK, (await liberado.GetAsync("/api/vehiculos")).StatusCode);

        // La provisoria ya no sirve; la propia sí, y sin marca.
        using var anonimo = _api.CreateClient();
        var conVieja = await anonimo.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner@provisoria.uy", Provisoria));
        Assert.Equal(HttpStatusCode.Unauthorized, conVieja.StatusCode);
        Assert.False((await _api.LoginAsync("owner@provisoria.uy", Propia)).Usuario.DebeCambiarPassword);
    }

    [Fact]
    public async Task No_se_puede_repetir_la_provisoria_ni_cambiarla_sin_saber_la_actual()
    {
        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        await admin.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "provisoria-mala", "Provisoria mala", null, "owner@provisoria-mala.uy", "Owner", Provisoria, CodigosDePlan.Demanda));

        using var cliente = Cliente(await _api.LoginAsync("owner@provisoria-mala.uy", Provisoria));

        var repetida = await cliente.PostAsJsonAsync("/api/auth/password", new CambiarPasswordPropiaRequest(Provisoria, Provisoria));
        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);

        var sinSaber = await cliente.PostAsJsonAsync("/api/auth/password", new CambiarPasswordPropiaRequest("cualquier-cosa-1A", Propia));
        Assert.Equal(HttpStatusCode.BadRequest, sinSaber.StatusCode);
    }

    [Fact]
    public async Task El_vendedor_que_da_de_alta_el_duenio_tambien_arranca_con_provisoria_y_el_duenio_no()
    {
        var (_, owner) = _api.AutomotoraConPlan("provisoria-vendedor", CodigosDePlan.Full);

        using var duenio = await _api.ClienteDeAsync(owner);
        var alta = await duenio.PostAsJsonAsync("/api/users", new CrearUsuarioRequest(
            "vendedor@provisoria-vendedor.uy", "Vendedor", Provisoria, "Seller"));
        var vendedor = await alta.Content.ReadFromJsonAsync<UsuarioDto>();

        Assert.True(vendedor!.DebeCambiarPassword);
        Assert.True((await _api.LoginAsync("vendedor@provisoria-vendedor.uy", Provisoria)).Usuario.DebeCambiarPassword);

        // El dueño cambiándose la suya por el endpoint de usuarios no queda marcado.
        var propia = (await _api.LoginAsync(owner)).Usuario.Id;
        await duenio.PostAsJsonAsync($"/api/users/{propia}/password", new CambiarPasswordRequest(Propia));
        Assert.False((await _api.LoginAsync(owner, Propia)).Usuario.DebeCambiarPassword);
    }

    private HttpClient Cliente(SesionDto sesion)
    {
        var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.AccessToken);
        return cliente;
    }
}
