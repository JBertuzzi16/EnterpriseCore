using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Informes;
using GenteFit.Persistencia.Repositorios;

namespace GenteFit.TestsAdmin;

// Informes calculados sobre datos conocidos en una base de datos temporal.
//
// Escenario (semana de pruebas, aforo 16 en todas las sesiones):
//   - Spinning lunes : 16 confirmadas + 2 en espera + 1 anulada  -> 100 %
//   - Spinning martes:  8 confirmadas                            ->  50 %
//   - Yoga miércoles :  4 confirmadas                            ->  25 %
//   - Yoga jueves    : sesión ANULADA con 5 reservas              -> no cuenta
//   - Pilates (mes anterior): 10 confirmadas                     -> fuera del periodo
public class EstadisticasRepositorioTests : BaseDatosPrueba
{
    private static readonly DateTime Lunes = new(2026, 11, 2);
    private readonly EstadisticasRepositorio _estadisticas;

    public EstadisticasRepositorioTests()
    {
        _estadisticas = new EstadisticasRepositorio(Contexto);
        CrearEscenario();
    }

    [Fact]
    public void Resumen_CuentaSoloSesionesNoAnuladasDelPeriodo()
    {
        ResumenEstadisticas r = _estadisticas.Resumen(Lunes, Lunes.AddDays(6));

        Assert.Equal(3, r.SesionesProgramadas);
        Assert.Equal(28, r.ReservasConfirmadas);   // 16 + 8 + 4
        Assert.Equal(2, r.ReservasEnEspera);
        Assert.Equal(1, r.ReservasAnuladas);
    }

    [Fact]
    public void Resumen_OcupacionMedia_EsPlazasOcupadasEntrePlazasOfertadas()
    {
        ResumenEstadisticas r = _estadisticas.Resumen(Lunes, Lunes.AddDays(6));

        // 28 plazas ocupadas de 48 ofertadas = 58,3 %
        Assert.Equal(58.3, r.OcupacionMediaPorcentaje);
        // 1 anulada de 31 reservas = 3,2 %
        Assert.Equal(3.2, r.TasaAnulacionPorcentaje);
    }

    [Fact]
    public void OcupacionPorSesion_MarcaLaSesionCompleta()
    {
        List<OcupacionSesion> sesiones = _estadisticas.OcupacionPorSesion(Lunes, Lunes.AddDays(6));

        OcupacionSesion lunes = sesiones.First();
        Assert.True(lunes.Completa);
        Assert.Equal(100, lunes.OcupacionPorcentaje);
        Assert.Equal(0, lunes.PlazasLibres);
        Assert.Equal(new[] { 100.0, 50.0, 25.0 }, sesiones.Select(s => s.OcupacionPorcentaje));
    }

    [Fact]
    public void ActividadesMasReservadas_OrdenaPorReservasConfirmadas()
    {
        List<RankingActividad> ranking = _estadisticas.ActividadesMasReservadas(Lunes, Lunes.AddDays(6));

        Assert.Equal(new[] { "Spinning", "Yoga" }, ranking.Select(a => a.Actividad));
        Assert.Equal(24, ranking[0].Confirmadas);
        Assert.Equal(2, ranking[0].Sesiones);
        Assert.Equal(75, ranking[0].OcupacionMediaPorcentaje); // 24 / 32
    }

    [Fact]
    public void OcupacionPorSala_AgrupaPorSala()
    {
        List<OcupacionSala> salas = _estadisticas.OcupacionPorSala(Lunes, Lunes.AddDays(6));

        OcupacionSala ciclo = salas.Single(s => s.Sala == "Sala Ciclo");
        Assert.Equal(2, ciclo.Sesiones);
        Assert.Equal(32, ciclo.PlazasOfertadas);
        Assert.Equal(24, ciclo.Confirmadas);
    }

    [Fact]
    public void PeriodoSinSesiones_DevuelveCeros()
    {
        ResumenEstadisticas r = _estadisticas.Resumen(new DateTime(2020, 1, 1), new DateTime(2020, 1, 31));

        Assert.Equal(0, r.SesionesProgramadas);
        Assert.Equal(0, r.OcupacionMediaPorcentaje);
        Assert.Empty(_estadisticas.ActividadesMasReservadas(new DateTime(2020, 1, 1), new DateTime(2020, 1, 31)));
    }

    [Fact]
    public void AltasClientesPorMes_DevuelveDoceMeses()
    {
        List<AltasMes> altas = _estadisticas.AltasClientesPorMes(2026);

        Assert.Equal(12, altas.Count);
        Assert.Equal(16, altas.Single(m => m.Mes == 10).Altas); // escenario: 16 clientes dados de alta en octubre
        Assert.Equal(4, altas.Single(m => m.Mes == 11).Altas);  // y 4 en noviembre
        Assert.Equal(0, altas.Single(m => m.Mes == 1).Altas);
    }

    // ---------- Escenario ----------

    private void CrearEscenario()
    {
        var usuarioMonitor = new Usuario { NombreUsuario = "monitor", Email = "monitor@gentefit.test", HashContrasena = "x", Rol = Rol.MONITOR };
        var monitor = new GenteFit.Persistencia.Entidades.Monitor
        {
            Dni = "11111111H", Nombre = "Laura", Apellidos = "Gómez", Email = usuarioMonitor.Email,
            Telefono = "600", Especialidad = "Fitness", Usuario = usuarioMonitor
        };

        var salaCiclo = new Sala { Nombre = "Sala Ciclo", Ubicacion = "Planta 1" };
        var salaZen = new Sala { Nombre = "Sala Zen", Ubicacion = "Planta 2" };
        var spinning = new Actividad { Nombre = "Spinning", Descripcion = "Ciclo", DuracionMinutos = 45, Intensidad = NivelIntensidad.ALTA };
        var yoga = new Actividad { Nombre = "Yoga", Descripcion = "Relax", DuracionMinutos = 60, Intensidad = NivelIntensidad.BAJA };
        var pilates = new Actividad { Nombre = "Pilates", Descripcion = "Core", DuracionMinutos = 50, Intensidad = NivelIntensidad.MEDIA };

        // 20 clientes: 16 dados de alta en octubre y 4 en noviembre.
        var clientes = Enumerable.Range(1, 20).Select(i => new Cliente
        {
            Dni = $"{50000000 + i}X",
            Nombre = $"Cliente{i}",
            Apellidos = "Prueba",
            Email = $"c{i}@gentefit.test",
            Telefono = "600",
            FechaNacimiento = new DateTime(1990, 1, 1),
            FechaAlta = i <= 16 ? new DateTime(2026, 10, i) : new DateTime(2026, 11, i - 16),
            Usuario = new Usuario { NombreUsuario = $"c{i}", Email = $"c{i}@gentefit.test", HashContrasena = "x", Rol = Rol.CLIENTE }
        }).ToList();
        Contexto.Clientes.AddRange(clientes);

        Sesion Sesion(Actividad a, Sala s, DateTime inicio, bool anulada = false) => new()
        {
            Actividad = a, Sala = s, Monitor = monitor, FechaHoraInicio = inicio,
            FechaHoraFin = inicio.AddMinutes(a.DuracionMinutos), AforoMaximo = 16, Anulada = anulada
        };

        void Reservas(Sesion sesion, int confirmadas, int enEspera = 0, int anuladas = 0)
        {
            int i = 0;
            for (int n = 0; n < confirmadas; n++) sesion.Reservas.Add(Nueva(clientes[i++], EstadoReserva.CONFIRMADA));
            for (int n = 0; n < enEspera; n++) sesion.Reservas.Add(Nueva(clientes[i++], EstadoReserva.EN_ESPERA));
            for (int n = 0; n < anuladas; n++) sesion.Reservas.Add(Nueva(clientes[i++], EstadoReserva.ANULADA));

            Reserva Nueva(Cliente c, EstadoReserva estado) => new()
            {
                Cliente = c, Estado = estado, FechaHoraReserva = sesion.FechaHoraInicio.AddDays(-2),
                FechaHoraAnulacion = estado == EstadoReserva.ANULADA ? sesion.FechaHoraInicio.AddDays(-1) : null
            };
        }

        Sesion s1 = Sesion(spinning, salaCiclo, Lunes.AddHours(18));
        Sesion s2 = Sesion(spinning, salaCiclo, Lunes.AddDays(1).AddHours(18));
        Sesion s3 = Sesion(yoga, salaZen, Lunes.AddDays(2).AddHours(9));
        Sesion s4 = Sesion(yoga, salaZen, Lunes.AddDays(3).AddHours(9), anulada: true);
        Sesion s5 = Sesion(pilates, salaZen, Lunes.AddDays(-20).AddHours(10));

        Reservas(s1, 16, enEspera: 2, anuladas: 1);
        Reservas(s2, 8);
        Reservas(s3, 4);
        Reservas(s4, 5);
        Reservas(s5, 10);

        Contexto.Sesiones.AddRange(s1, s2, s3, s4, s5);
        Contexto.SaveChanges();
    }
}
