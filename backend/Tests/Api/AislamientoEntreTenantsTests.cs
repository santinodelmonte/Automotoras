using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Users;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// El criterio de aceptación número uno de la fase 1: un usuario del tenant A no puede
/// acceder a ningún dato del tenant B por ninguna vía, incluyendo manipular el id en la
/// URL.
/// </summary>
/// <remarks>
/// Estos tests van contra la API completa, no contra el <c>DbContext</c>. Es la
/// diferencia entre "la consulta filtra bien" y "el endpoint responde 404", que es lo
/// único que le consta a quien está del otro lado.
/// </remarks>
public sealed class AislamientoEntreTenantsTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public AislamientoEntreTenantsTests(FabricaDeApi api)
    {
        _api = api;
    }

    /// <summary>
    /// El caso del criterio de aceptación, tal cual: el id existe, el token es válido, y
    /// el recurso es de otra automotora.
    /// </summary>
    [Fact]
    public async Task Pedir_por_id_un_usuario_de_otro_tenant_responde_404()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.GetAsync($"/api/users/{_api.OwnerDeSur}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Editar_un_usuario_de_otro_tenant_responde_404()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/users/{_api.OwnerDeSur}",
            new ActualizarUsuarioRequest("Nombre cambiado", Activo: false));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cambiarle_la_contrasena_a_un_usuario_de_otro_tenant_responde_404()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/users/{_api.OwnerDeSur}/password",
            new CambiarPasswordRequest("Otra-clave-9"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_listado_solo_trae_usuarios_del_propio_tenant()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioDto>>("/api/users");

        Assert.NotNull(usuarios);
        Assert.NotEmpty(usuarios);
        Assert.All(usuarios, u => Assert.Equal(_api.TenantNorte, u.TenantId));
        Assert.DoesNotContain(usuarios, u => u.Email == FabricaDeApi.EmailOwnerSur);

        // El SuperAdmin tampoco: tiene tenant nulo y se administra por /api/admin/*.
        Assert.DoesNotContain(usuarios, u => u.Email == FabricaDeApi.EmailSuperAdmin);
    }

    /// <summary>
    /// El alta no acepta el tenant desde el request: lo sella el servidor con el del
    /// token. Es lo que hace que no exista forma de crearse un usuario adentro de otra
    /// automotora.
    /// </summary>
    [Fact]
    public async Task El_alta_sella_el_tenant_del_token()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/users",
            new CrearUsuarioRequest("nuevo@norte.uy", "Vendedor Nuevo", "Clave-nueva-7", Roles.Seller));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<UsuarioDto>();
        Assert.NotNull(creado);
        Assert.Equal(_api.TenantNorte, creado.TenantId);
        Assert.Equal(Roles.Seller, creado.Rol);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var enLaBase = await db.Users
            .IgnoreQueryFilters()
            .SingleAsync(u => u.Id == creado.Id);

        Assert.Equal(_api.TenantNorte, enLaBase.TenantId);
    }

    [Fact]
    public async Task El_email_ya_usado_en_otra_automotora_no_se_puede_repetir()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/users",
            new CrearUsuarioRequest(FabricaDeApi.EmailOwnerSur, "Colado", "Clave-nueva-7", Roles.Seller));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_vendedor_no_administra_usuarios()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// El SuperAdmin es cross-tenant, pero no por los endpoints normales: su token no
    /// lleva tenant, así que no tiene rol de Owner en ninguna automotora.
    /// </summary>
    [Fact]
    public async Task El_superadmin_no_entra_por_los_endpoints_de_tenant()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await cliente.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_owner_no_puede_darse_de_baja_a_si_mismo()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/users/{_api.OwnerDeNorte}",
            new ActualizarUsuarioRequest("Owner Norte", Activo: false));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    /// <summary>
    /// El rol viaja adentro de la firma del token. Mandarlo por header o por query param
    /// no cambia nada: nadie lo lee de ahí.
    /// </summary>
    [Fact]
    public async Task Mandar_el_tenant_por_header_no_cambia_el_tenant_del_request()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        cliente.DefaultRequestHeaders.Add("X-Tenant-Id", _api.TenantSur.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioDto>>("/api/users");

        Assert.NotNull(usuarios);
        Assert.All(usuarios, u => Assert.Equal(_api.TenantNorte, u.TenantId));
    }

    /// <summary>
    /// Y el slug de la ruta tampoco: en el panel privado el tenant sale del token y de
    /// ningún otro lado.
    /// </summary>
    [Fact]
    public async Task El_slug_en_la_ruta_no_le_gana_al_token()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioDto>>("/t/sur/api/users");

        Assert.NotNull(usuarios);
        Assert.All(usuarios, u => Assert.Equal(_api.TenantNorte, u.TenantId));
    }
}
