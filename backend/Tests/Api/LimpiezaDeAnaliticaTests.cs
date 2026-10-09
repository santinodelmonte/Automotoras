using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// El detalle de visitas y búsquedas no se guarda para siempre: pasado el plazo se borra.
/// </summary>
public sealed class LimpiezaDeAnaliticaTests : IClassFixture<FabricaDeApi>
{
    private const string Url = "/api/jobs/limpieza-de-analitica";

    private readonly FabricaDeApi _api;

    public LimpiezaDeAnaliticaTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Borra_lo_viejo_y_deja_lo_reciente_de_todas_las_automotoras()
    {
        var viejo = DateTime.UtcNow.AddMonths(-(RetencionDeAnalitica.MesesPorDefecto + 1));
        var reciente = DateTime.UtcNow.AddMonths(-11);

        _api.ConLaBase(db =>
        {
            db.Eventos.AddRange(
                Evento(_api.TenantNorte, viejo, "limpieza-vieja"),
                Evento(_api.TenantSur, viejo, "limpieza-vieja"),
                Evento(_api.TenantNorte, reciente, "limpieza-reciente"));

            db.Busquedas.Add(new Busqueda
            {
                TenantId = _api.TenantNorte,
                Filtros = "{}",
                SessionId = "limpieza-vieja",
                CreatedAt = viejo,
            });
        });

        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Job-Secret", FabricaDeApi.SecretoDeJobs);

        var respuesta = await cliente.PostAsync(Url, content: null);
        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoDeLimpiezaDto>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(resultado!.EventosBorrados >= 2);
        Assert.True(resultado.BusquedasBorradas >= 1);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.Eventos.IgnoreQueryFilters().AnyAsync(e => e.SessionId == "limpieza-vieja"));
        Assert.False(await db.Busquedas.IgnoreQueryFilters().AnyAsync(b => b.SessionId == "limpieza-vieja"));
        Assert.True(await db.Eventos.IgnoreQueryFilters().AnyAsync(e => e.SessionId == "limpieza-reciente"));
    }

    [Fact]
    public async Task Sin_el_secreto_responde_401()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsync(Url, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    private static Evento Evento(int tenantId, DateTime cuando, string sesion) => new()
    {
        TenantId = tenantId,
        Tipo = TipoEvento.ViewListado,
        SessionId = sesion,
        CreatedAt = cuando,
    };
}
