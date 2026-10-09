using AutomotoraSaaS.Core.Publico;

namespace AutomotoraSaaS.Tests.Publico;

public sealed class InterpreteDeBusquedaTests
{
    private const int Toyota = 1, Peugeot = 2, Ford = 3, Chevrolet = 4, Chery = 5, Citroen = 6;
    private const int Corolla = 10, CorollaCross = 11, Hilux = 12, P2008 = 20, Ka = 30, Kuga = 31, Onix = 40, Tiggo = 50, CElysee = 60;

    private static readonly EntradaDelCatalogo[] Catalogo =
    [
        new(Toyota, "Toyota", Corolla, "Corolla"),
        new(Toyota, "Toyota", CorollaCross, "Corolla Cross"),
        new(Toyota, "Toyota", Hilux, "Hilux"),
        new(Peugeot, "Peugeot", P2008, "2008"),
        new(Ford, "Ford", Ka, "Ka"),
        new(Ford, "Ford", Kuga, "Kuga"),
        new(Chevrolet, "Chevrolet", Onix, "Onix"),
        new(Chery, "Chery", Tiggo, "Tiggo 4"),
        new(Citroen, "Citroën", CElysee, "C-Elysée"),
    ];

    [Theory]
    [InlineData("Toyota Hilux", Toyota, Hilux)]
    [InlineData("hilux", Toyota, Hilux)]
    [InlineData("  TOYOTA   corolla cross ", Toyota, CorollaCross)]
    [InlineData("corolla", Toyota, Corolla)]
    [InlineData("toyo", Toyota, null)]
    [InlineData("toyota hil", Toyota, Hilux)]
    [InlineData("citroen c elysee", Citroen, CElysee)]
    [InlineData("Ford Ka 2015", Ford, Ka)]
    [InlineData("peugeot 2008", Peugeot, P2008)]
    public void Reconoce_marca_y_modelo(string texto, int marca, int? modelo)
    {
        var resultado = InterpreteDeBusqueda.Interpretar(texto, Catalogo);

        Assert.Equal(marca, resultado.MarcaId);
        Assert.Equal(modelo, resultado.ModeloId);
    }

    /// <summary>"che" es tanto Chevrolet como Chery: adivinar ensuciaría el reporte.</summary>
    [Theory]
    [InlineData("che")]
    [InlineData("tesla model 3")]
    [InlineData("auto barato")]
    [InlineData("to")]
    [InlineData("")]
    public void Ante_la_duda_no_adivina(string texto)
    {
        var resultado = InterpreteDeBusqueda.Interpretar(texto, Catalogo);

        Assert.Null(resultado.MarcaId);
        Assert.Null(resultado.ModeloId);
    }

    /// <summary>"Ka" es una palabra entera: no tiene que aparecer adentro de "Kangoo" ni de "Kuga".</summary>
    [Fact]
    public void Un_modelo_corto_no_se_encuentra_adentro_de_otra_palabra()
    {
        var resultado = InterpreteDeBusqueda.Interpretar("ford kuga", Catalogo);

        Assert.Equal(Kuga, resultado.ModeloId);
    }

    [Fact]
    public void Normalizar_saca_tildes_mayusculas_y_signos()
    {
        Assert.Equal("citroen c elysee", InterpreteDeBusqueda.Normalizar("  Citroën  C-Elysée!"));
    }
}
