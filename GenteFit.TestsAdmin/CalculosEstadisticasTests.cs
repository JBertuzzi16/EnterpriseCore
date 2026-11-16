using GenteFit.Persistencia.Informes;

namespace GenteFit.TestsAdmin;

// Cálculos de los informes (sin base de datos).
public class CalculosEstadisticasTests
{
    [Theory]
    [InlineData(8, 16, 50.0)]
    [InlineData(1, 3, 33.3)]
    [InlineData(2, 3, 66.7)]
    [InlineData(16, 16, 100.0)]
    [InlineData(0, 16, 0.0)]
    public void Porcentaje_RedondeaAUnDecimal(int parte, int total, double esperado)
    {
        Assert.Equal(esperado, CalculosEstadisticas.Porcentaje(parte, total));
    }

    [Fact]
    public void Porcentaje_TotalCero_NoDivideEntreCero()
    {
        Assert.Equal(0, CalculosEstadisticas.Porcentaje(5, 0));
    }

    [Fact]
    public void OcupacionMedia_SeCalculaSobreElTotalDePlazas()
    {
        // 16/16 y 0/16 -> 16 de 32 = 50 %
        Assert.Equal(50, CalculosEstadisticas.OcupacionMedia(new[] { (16, 16), (0, 16) }));
    }

    [Fact]
    public void OcupacionMedia_NoSuperaElCienPorCien()
    {
        // Si hubiera más confirmadas que aforo (no debería pasar), se cuenta el aforo.
        Assert.Equal(100, CalculosEstadisticas.OcupacionMedia(new[] { (20, 16) }));
    }

    [Fact]
    public void OcupacionMedia_SinSesiones_EsCero()
    {
        Assert.Equal(0, CalculosEstadisticas.OcupacionMedia(Array.Empty<(int, int)>()));
    }

    [Fact]
    public void CompletarMeses_RellenaLosMesesSinAltas()
    {
        var meses = CalculosEstadisticas.CompletarMeses(new Dictionary<int, int> { [3] = 5, [11] = 2 });

        Assert.Equal(12, meses.Count);
        Assert.Equal(5, meses[2].Altas);
        Assert.Equal(0, meses[0].Altas);
        Assert.Equal("Marzo", meses[2].NombreMes);
    }

    [Fact]
    public void NormalizarPeriodo_IncluyeElUltimoDiaYCorrigeElOrden()
    {
        var (desde, hastaExclusivo) = CalculosEstadisticas.NormalizarPeriodo(
            new DateTime(2026, 11, 30, 15, 0, 0), new DateTime(2026, 11, 1));

        Assert.Equal(new DateTime(2026, 11, 1), desde);
        Assert.Equal(new DateTime(2026, 12, 1), hastaExclusivo);
    }

    [Theory]
    [InlineData(5, 10, 50.0)]
    [InlineData(10, 10, 100.0)]
    [InlineData(3, 0, 0.0)]
    public void AnchoBarra_EsProporcionalAlMaximo(double valor, double maximo, double esperado)
    {
        Assert.Equal(esperado, CalculosEstadisticas.AnchoBarra(valor, maximo));
    }
}
