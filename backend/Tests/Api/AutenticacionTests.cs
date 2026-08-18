using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Auth;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Login, renovación y cierre de sesión contra la API levantada de verdad.
/// </summary>
public sealed class AutenticacionTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public AutenticacionTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_login_devuelve_tokens_y_el_usuario()
    {
        var sesion = await _api.LoginAsync(FabricaDeApi.EmailOwnerNorte);

        Assert.False(string.IsNullOrWhiteSpace(sesion.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(sesion.RefreshToken));
        Assert.True(sesion.ExpiraEn > DateTimeOffset.UtcNow);
        Assert.Equal(FabricaDeApi.EmailOwnerNorte, sesion.Usuario.Email);
        Assert.Equal(Roles.Owner, sesion.Usuario.Rol);
        Assert.Equal(_api.TenantNorte, sesion.Usuario.TenantId);
    }

    [Fact]
    public async Task Con_la_contrasena_equivocada_responde_401()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(FabricaDeApi.EmailOwnerNorte, "no-es-esta-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /// <summary>
    /// Un email inexistente y una contraseña equivocada tienen que responder igual. Si se
    /// distinguieran, el login serviría para averiguar qué cuentas existen.
    /// </summary>
    [Fact]
    public async Task Con_un_email_inexistente_responde_401_igual()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("nadie@ninguna.uy", FabricaDeApi.Password));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_usuario_dado_de_baja_no_puede_entrar()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(FabricaDeApi.EmailInactivoNorte, FabricaDeApi.Password));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_email_sin_formato_valido_responde_400_con_los_errores()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest("no-es-un-email", "x"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_token_los_endpoints_privados_responden_401()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_un_token_inventado_responde_401()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "esto.no.es-un-token");

        var respuesta = await cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Me_devuelve_el_usuario_del_token()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var usuario = await cliente.GetFromJsonAsync<UsuarioDto>("/api/auth/me");

        Assert.NotNull(usuario);
        Assert.Equal(_api.VendedorDeNorte, usuario.Id);
        Assert.Equal(_api.TenantNorte, usuario.TenantId);
        Assert.Equal(Roles.Seller, usuario.Rol);
    }

    /// <summary>El SuperAdmin no pertenece a ninguna automotora: su token no lleva tenant.</summary>
    [Fact]
    public async Task El_superadmin_no_tiene_tenant()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var usuario = await cliente.GetFromJsonAsync<UsuarioDto>("/api/auth/me");

        Assert.NotNull(usuario);
        Assert.Null(usuario.TenantId);
        Assert.Equal(Roles.SuperAdmin, usuario.Rol);
    }

    [Fact]
    public async Task El_refresh_rota_el_token_y_el_anterior_deja_de_servir()
    {
        using var cliente = _api.CreateClient();

        var sesion = await _api.LoginAsync(FabricaDeApi.EmailOwnerNorte);

        var renovada = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(sesion.RefreshToken));
        renovada.EnsureSuccessStatusCode();

        var nueva = await renovada.Content.ReadFromJsonAsync<SesionDto>();
        Assert.NotNull(nueva);
        Assert.NotEqual(sesion.RefreshToken, nueva.RefreshToken);

        // El token ya canjeado no vuelve a servir.
        var reintento = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(sesion.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reintento.StatusCode);
    }

    /// <summary>
    /// Reusar un refresh token ya canjeado corta todas las sesiones del usuario, no solo
    /// la del intento: si el token viejo aparece, o se filtró o alguien está reproduciendo
    /// una sesión, y en los dos casos lo prudente es echar a todos.
    /// </summary>
    [Fact]
    public async Task Reusar_un_refresh_revocado_invalida_tambien_al_vigente()
    {
        using var cliente = _api.CreateClient();

        var sesion = await _api.LoginAsync(FabricaDeApi.EmailOwnerSur);

        var primera = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(sesion.RefreshToken));
        primera.EnsureSuccessStatusCode();
        var vigente = await primera.Content.ReadFromJsonAsync<SesionDto>();
        Assert.NotNull(vigente);

        var reuso = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(sesion.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuso.StatusCode);

        var conElVigente = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(vigente.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, conElVigente.StatusCode);
    }

    [Fact]
    public async Task El_logout_revoca_el_refresh_token()
    {
        using var cliente = _api.CreateClient();

        var sesion = await _api.LoginAsync(FabricaDeApi.EmailVendedorNorte);

        var salida = await cliente.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(sesion.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, salida.StatusCode);

        var reintento = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(sesion.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reintento.StatusCode);
    }
}
