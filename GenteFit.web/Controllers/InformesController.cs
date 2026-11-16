using System.Globalization;
using System.Text;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Informes;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Web.Models.Admin;
using GenteFit.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenteFit.Web.Controllers;

// Estadísticas e informes del centro (funcionalidad avanzada del administrador).
[Authorize(Roles = nameof(Rol.ADMIN))]
public class InformesController : Controller
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    private readonly EstadisticasRepositorio _estadisticas;
    private readonly ConfiguracionCentroServicio _configuracion;

    public InformesController(EstadisticasRepositorio estadisticas, ConfiguracionCentroServicio configuracion)
    {
        _estadisticas = estadisticas;
        _configuracion = configuracion;
    }

    [HttpGet]
    public IActionResult Index(DateTime? desde, DateTime? hasta, int? anio)
    {
        var (inicio, fin) = Periodo(desde, hasta);

        var modelo = new InformesViewModel
        {
            Desde = inicio,
            Hasta = fin,
            Anio = anio ?? DateTime.Today.Year,
            Resumen = _estadisticas.Resumen(inicio, fin),
            Actividades = _estadisticas.ActividadesMasReservadas(inicio, fin),
            Salas = _estadisticas.OcupacionPorSala(inicio, fin),
            Sesiones = _estadisticas.OcupacionPorSesion(inicio, fin),
            AltasPorMes = _estadisticas.AltasClientesPorMes(anio ?? DateTime.Today.Year),
            Centro = _configuracion.Obtener()
        };

        return View(modelo);
    }

    // Exporta un informe a CSV (separador ";" y UTF-8 con BOM para que Excel en español lo abra bien).
    [HttpGet]
    public IActionResult Exportar(string informe, DateTime? desde, DateTime? hasta, int? anio)
    {
        var (inicio, fin) = Periodo(desde, hasta);
        var csv = new StringBuilder();
        string nombre;

        switch (informe)
        {
            case "actividades":
                nombre = "actividades-mas-reservadas";
                csv.AppendLine("Actividad;Sesiones;Reservas confirmadas;En lista de espera;Ocupación media (%)");
                foreach (RankingActividad a in _estadisticas.ActividadesMasReservadas(inicio, fin, int.MaxValue))
                    csv.AppendLine(Fila(a.Actividad, a.Sesiones, a.Confirmadas, a.EnEspera, a.OcupacionMediaPorcentaje));
                break;

            case "salas":
                nombre = "ocupacion-salas";
                csv.AppendLine("Sala;Sesiones;Plazas ofertadas;Plazas ocupadas;Ocupación (%)");
                foreach (OcupacionSala s in _estadisticas.OcupacionPorSala(inicio, fin))
                    csv.AppendLine(Fila(s.Sala, s.Sesiones, s.PlazasOfertadas, s.Confirmadas, s.OcupacionPorcentaje));
                break;

            case "altas":
                int anioAltas = anio ?? DateTime.Today.Year;
                nombre = $"altas-clientes-{anioAltas}";
                csv.AppendLine("Mes;Altas");
                foreach (AltasMes m in _estadisticas.AltasClientesPorMes(anioAltas))
                    csv.AppendLine(Fila(m.NombreMes, m.Altas));
                break;

            default:
                nombre = "ocupacion-sesiones";
                csv.AppendLine("Fecha;Hora;Actividad;Sala;Aforo;Confirmadas;En espera;Anuladas;Ocupación (%)");
                foreach (OcupacionSesion s in _estadisticas.OcupacionPorSesion(inicio, fin))
                    csv.AppendLine(Fila(s.Inicio.ToString("dd/MM/yyyy"), s.Inicio.ToString("HH:mm"), s.Actividad, s.Sala,
                        s.Aforo, s.Confirmadas, s.EnEspera, s.Anuladas, s.OcupacionPorcentaje));
                break;
        }

        byte[] contenido = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        string fichero = informe == "altas"
            ? $"{nombre}.csv"
            : $"{nombre}_{inicio:yyyyMMdd}-{fin:yyyyMMdd}.csv";

        return File(contenido, "text/csv", fichero);
    }

    // Por defecto, los últimos 30 días.
    private static (DateTime Desde, DateTime Hasta) Periodo(DateTime? desde, DateTime? hasta)
    {
        DateTime fin = (hasta ?? DateTime.Today).Date;
        DateTime inicio = (desde ?? fin.AddDays(-29)).Date;
        return inicio <= fin ? (inicio, fin) : (fin, inicio);
    }

    private static string Fila(params object[] valores) =>
        string.Join(';', valores.Select(v => v switch
        {
            double d => d.ToString("0.0", Es),
            string s => Celda(s),
            _ => Convert.ToString(v, Es) ?? ""
        }));

    // Entrecomilla los textos que llevan ";" o comillas, y evita que Excel ejecute fórmulas.
    private static string Celda(string texto)
    {
        if (texto.Length > 0 && "=+-@".Contains(texto[0]))
            texto = "'" + texto;

        return texto.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
            ? "\"" + texto.Replace("\"", "\"\"") + "\""
            : texto;
    }
}
