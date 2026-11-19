using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Web.Servicios;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenteFit.TestsAdmin;

// Configuración del centro guardada en fichero (carpeta temporal, sin base de datos).
public class ConfiguracionCentroServicioTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "GenteFitTest_" + Guid.NewGuid().ToString("N"));

    private ConfiguracionCentroServicio NuevoServicio() =>
        new(_carpeta, NullLogger<ConfiguracionCentroServicio>.Instance);

    [Fact]
    public void SinFichero_DevuelveValoresPorDefecto()
    {
        ConfiguracionCentro c = NuevoServicio().Obtener();

        Assert.Equal("GenteFit", c.NombreCentro);
        Assert.Equal(new TimeOnly(7, 0), c.HoraApertura);
    }

    [Fact]
    public void Guardar_SeConservaAlVolverAArrancar()
    {
        NuevoServicio().Guardar(new ConfiguracionCentro
        {
            NombreCentro = "GenteFit Sant Cugat",
            RutaFuenteXml = @"C:\GenteFit\actividades.xml",
            HoraApertura = new TimeOnly(8, 0),
            HoraCierre = new TimeOnly(21, 30)
        });

        ConfiguracionCentro leida = NuevoServicio().Obtener(); // servicio nuevo = aplicación reiniciada

        Assert.Equal("GenteFit Sant Cugat", leida.NombreCentro);
        Assert.Equal(@"C:\GenteFit\actividades.xml", leida.RutaFuenteXml);
        Assert.Equal(new TimeOnly(21, 30), leida.HoraCierre);
    }

    [Fact]
    public void FicheroDanado_NoRompeLaAplicacion()
    {
        Directory.CreateDirectory(_carpeta);
        File.WriteAllText(Path.Combine(_carpeta, ConfiguracionCentroServicio.NombreFichero), "{ esto no es json");

        ConfiguracionCentro c = NuevoServicio().Obtener();

        Assert.Equal("GenteFit", c.NombreCentro);
    }

    [Fact]
    public void HorarioIncoherente_SeRechaza()
    {
        Assert.Throws<ArgumentException>(() => NuevoServicio().Guardar(new ConfiguracionCentro
        {
            HoraApertura = new TimeOnly(22, 0),
            HoraCierre = new TimeOnly(7, 0)
        }));
    }

    [Fact]
    public void Obtener_DevuelveUnaCopia()
    {
        var servicio = NuevoServicio();
        servicio.Obtener().NombreCentro = "Cambiado sin guardar";

        Assert.Equal("GenteFit", servicio.Obtener().NombreCentro);
    }

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
            Directory.Delete(_carpeta, recursive: true);
    }
}

// Preparación de la base de datos que hace el instalador (con SQL Server).
public class PreparacionBaseDatosTests : BaseDatosPrueba
{
    // Sin tablas: la preparación crea la base de datos con las migraciones, como en una instalación nueva.
    public PreparacionBaseDatosTests() : base(crearTablas: false) { }

    [Fact]
    public void Preparar_CreaElAdministradorIndicado()
    {
        PreparacionBaseDatos.Preparar(Contexto, "direccion@gentefit.test", "Admin2026", cargarDatosDemo: false);

        Usuario? admin = new UsuarioRepositorio(ContextoNuevo()).ValidarCredenciales("direccion@gentefit.test", "Admin2026");
        Assert.NotNull(admin);
        Assert.Equal(Rol.ADMIN, admin.Rol);
    }

    [Fact]
    public void Preparar_DosVeces_NoDuplicaNada()
    {
        PreparacionBaseDatos.Preparar(Contexto, "direccion@gentefit.test", "Admin2026", cargarDatosDemo: true);
        int usuarios = ContextoNuevo().Usuarios.Count();
        int sesiones = ContextoNuevo().Sesiones.Count();

        List<string> pasos = PreparacionBaseDatos.Preparar(ContextoNuevo(), "direccion@gentefit.test", "Admin2026", cargarDatosDemo: true);

        Assert.Equal(usuarios, ContextoNuevo().Usuarios.Count());
        Assert.Equal(sesiones, ContextoNuevo().Sesiones.Count());
        Assert.Contains(pasos, p => p.StartsWith("Ya existe una cuenta"));
    }

    [Fact]
    public void Preparar_ConDatosDemo_LosInformesTienenContenido()
    {
        PreparacionBaseDatos.Preparar(Contexto, null, null, cargarDatosDemo: true);

        var resumen = new EstadisticasRepositorio(ContextoNuevo()).Resumen(DateTime.Today.AddDays(-27), DateTime.Today);

        Assert.True(resumen.SesionesProgramadas > 0);
        Assert.True(resumen.ReservasConfirmadas > 0);
    }

    [Fact]
    public void Preparar_ContrasenaDebil_NoCreaElAdministrador()
    {
        Assert.Throws<ArgumentException>(() =>
            PreparacionBaseDatos.Preparar(Contexto, "direccion@gentefit.test", "123", cargarDatosDemo: false));

        Assert.False(ContextoNuevo().Usuarios.Any(u => u.Email == "direccion@gentefit.test"));
    }
}
