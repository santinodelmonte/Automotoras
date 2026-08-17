using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Tests.Persistence;

/// <summary>
/// Aislamiento de lectura entre tenants. Es el cimiento del producto: si esto falla,
/// todo lo que se apoye encima hay que rehacerlo.
/// </summary>
public sealed class FiltrosGlobalesTests : IClassFixture<BaseDeDatosDePrueba>
{
    private readonly BaseDeDatosDePrueba _db;

    public FiltrosGlobalesTests(BaseDeDatosDePrueba db)
    {
        _db = db;
    }

    /// <summary>
    /// Red de contención contra olvidos: los filtros están escritos uno por uno en
    /// <c>OnModelCreating</c>, y una entidad nueva que implemente <c>ITenantEntity</c>
    /// podría quedar sin filtro sin que nadie lo note. Este test falla si eso pasa.
    /// </summary>
    [Fact]
    public void Toda_entidad_de_tenant_tiene_filtro_global()
    {
        using var contexto = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var sinFiltro = contexto.Model.GetEntityTypes()
            .Where(e => typeof(ITenantEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.True(
            sinFiltro.Count == 0,
            $"Estas entidades implementan ITenantEntity pero no tienen filtro global: {string.Join(", ", sinFiltro)}");
    }

    [Fact]
    public void Un_tenant_solo_ve_sus_vehiculos()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);
        using var contextoB = _db.CrearContexto(BaseDeDatosDePrueba.TenantB);

        var deA = contextoA.Vehiculos.Select(v => v.TenantId).ToList();
        var deB = contextoB.Vehiculos.Select(v => v.TenantId).ToList();

        Assert.All(deA, tenantId => Assert.Equal(BaseDeDatosDePrueba.TenantA, tenantId));
        Assert.All(deB, tenantId => Assert.Equal(BaseDeDatosDePrueba.TenantB, tenantId));
        Assert.NotEmpty(deA);
        Assert.NotEmpty(deB);
    }

    /// <summary>
    /// El caso del criterio de aceptación: manipular el id en la URL. El vehículo existe
    /// y el id es correcto, pero es de otro tenant, así que la consulta no lo encuentra
    /// y el endpoint termina devolviendo 404.
    /// </summary>
    [Fact]
    public void Pedir_por_id_un_vehiculo_de_otro_tenant_no_devuelve_nada()
    {
        using var contextoB = _db.CrearContexto(BaseDeDatosDePrueba.TenantB);

        var ajeno = contextoB.Vehiculos.SingleOrDefault(v => v.Id == _db.VehiculoDeA);

        Assert.Null(ajeno);
    }

    [Fact]
    public void Las_fotos_de_un_vehiculo_ajeno_son_invisibles()
    {
        using var contextoB = _db.CrearContexto(BaseDeDatosDePrueba.TenantB);

        var ajena = contextoB.VehiculoFotos.SingleOrDefault(f => f.Id == _db.FotoDeA);

        Assert.Null(ajena);
    }

    [Fact]
    public void Los_eventos_de_otro_tenant_son_invisibles()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var tenantIds = contextoA.Eventos.Select(e => e.TenantId).Distinct().ToList();

        Assert.Equal([BaseDeDatosDePrueba.TenantA], tenantIds);
    }

    [Fact]
    public void Los_usuarios_de_otro_tenant_son_invisibles()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var emails = contextoA.Users.Select(u => u.Email).ToList();

        Assert.Equal(["owner@norte.uy"], emails);
    }

    /// <summary>
    /// El SuperAdmin tiene tenant nulo. No aparece en las consultas de ningún tenant: se
    /// administra por <c>/api/admin/*</c>, no por los endpoints normales.
    /// </summary>
    [Fact]
    public void El_superadmin_no_aparece_en_las_consultas_de_un_tenant()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var haySuperAdmin = contextoA.Users.Any(u => u.TenantId == null);

        Assert.False(haySuperAdmin);
    }

    /// <summary>
    /// Fallar cerrado: sin tenant resuelto no se devuelve nada, en vez de devolverlo todo.
    /// Es la diferencia entre un bug de resolución que rompe una pantalla y uno que filtra
    /// la base entera.
    /// </summary>
    [Fact]
    public void Sin_tenant_resuelto_no_se_ve_nada()
    {
        using var contexto = _db.CrearContexto(tenantId: null);

        Assert.Empty(contexto.Vehiculos);
        Assert.Empty(contexto.Eventos);
        Assert.Empty(contexto.Users);
        Assert.Empty(contexto.VehiculoFotos);
    }

    /// <summary>
    /// El escape del SuperAdmin funciona, pero solo cuando se lo pide de forma explícita.
    /// </summary>
    [Fact]
    public void IgnoreQueryFilters_da_acceso_cross_tenant()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var todos = contextoA.Vehiculos.IgnoreQueryFilters().Count();

        Assert.Equal(2, todos);
    }

    /// <summary>
    /// El modelo de EF se cachea por tipo de contexto. Si el filtro se resolviera una sola
    /// vez al construir el modelo, todos los contextos posteriores verían los datos del
    /// primer tenant que consultó. Este test es el que descarta esa falla.
    /// </summary>
    [Fact]
    public void El_filtro_se_reevalua_en_cada_contexto_y_no_queda_fijado_al_primero()
    {
        using (var primero = _db.CrearContexto(BaseDeDatosDePrueba.TenantA))
        {
            Assert.Equal(_db.VehiculoDeA, primero.Vehiculos.Single().Id);
        }

        using var segundo = _db.CrearContexto(BaseDeDatosDePrueba.TenantB);

        Assert.Equal(_db.VehiculoDeB, segundo.Vehiculos.Single().Id);
    }
}
