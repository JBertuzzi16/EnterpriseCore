using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Informes;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Persistencia.Repositorios;

// Consultas de estadísticas para el administrador.
// Criterios:
//  - Una sesión pertenece al periodo según su fecha y hora de inicio.
//  - Las sesiones anuladas no cuentan (no se ofertaron plazas).
//  - La ocupación es plazas confirmadas / plazas ofertadas (aforo).
//  - Las altas de clientes se cuentan por su fecha de alta.
public class EstadisticasRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public EstadisticasRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    public ResumenEstadisticas Resumen(DateTime desde, DateTime hasta)
    {
        var (inicio, fin) = CalculosEstadisticas.NormalizarPeriodo(desde, hasta);
        List<OcupacionSesion> sesiones = OcupacionPorSesion(desde, hasta);

        int confirmadas = sesiones.Sum(s => s.Confirmadas);
        int enEspera = sesiones.Sum(s => s.EnEspera);
        int anuladas = sesiones.Sum(s => s.Anuladas);

        int clientesActivos = _contexto.Clientes.Count(c => c.Activo);
        int altas = _contexto.Clientes.Count(c => c.FechaAlta >= inicio && c.FechaAlta < fin);

        return new ResumenEstadisticas(
            ClientesActivos: clientesActivos,
            AltasEnPeriodo: altas,
            SesionesProgramadas: sesiones.Count,
            ReservasConfirmadas: confirmadas,
            ReservasEnEspera: enEspera,
            ReservasAnuladas: anuladas,
            OcupacionMediaPorcentaje: CalculosEstadisticas.OcupacionMedia(
                sesiones.Select(s => (s.Confirmadas, s.Aforo))),
            TasaAnulacionPorcentaje: CalculosEstadisticas.Porcentaje(
                anuladas, confirmadas + enEspera + anuladas));
    }

    public List<OcupacionSesion> OcupacionPorSesion(DateTime desde, DateTime hasta)
    {
        var (inicio, fin) = CalculosEstadisticas.NormalizarPeriodo(desde, hasta);

        var filas = _contexto.Sesiones
            .AsNoTracking()
            .Where(s => !s.Anulada && s.FechaHoraInicio >= inicio && s.FechaHoraInicio < fin)
            .OrderBy(s => s.FechaHoraInicio)
            .Select(s => new
            {
                s.Id,
                s.FechaHoraInicio,
                Actividad = s.Actividad.Nombre,
                Sala = s.Sala.Nombre,
                s.AforoMaximo,
                Confirmadas = s.Reservas.Count(r => r.Estado == EstadoReserva.CONFIRMADA),
                EnEspera = s.Reservas.Count(r => r.Estado == EstadoReserva.EN_ESPERA),
                Anuladas = s.Reservas.Count(r => r.Estado == EstadoReserva.ANULADA)
            })
            .ToList();

        return filas
            .Select(f => new OcupacionSesion(
                f.Id, f.FechaHoraInicio, f.Actividad, f.Sala, f.AforoMaximo,
                f.Confirmadas, f.EnEspera, f.Anuladas,
                CalculosEstadisticas.Porcentaje(Math.Min(f.Confirmadas, f.AforoMaximo), f.AforoMaximo)))
            .ToList();
    }

    public List<RankingActividad> ActividadesMasReservadas(DateTime desde, DateTime hasta, int maximo = 10)
    {
        return OcupacionPorSesion(desde, hasta)
            .GroupBy(s => s.Actividad)
            .Select(g => new RankingActividad(
                g.Key,
                g.Count(),
                g.Sum(s => s.Confirmadas),
                g.Sum(s => s.EnEspera),
                CalculosEstadisticas.OcupacionMedia(g.Select(s => (s.Confirmadas, s.Aforo)))))
            .OrderByDescending(a => a.Confirmadas)
            .ThenByDescending(a => a.EnEspera)
            .ThenBy(a => a.Actividad)
            .Take(Math.Max(1, maximo))
            .ToList();
    }

    public List<OcupacionSala> OcupacionPorSala(DateTime desde, DateTime hasta)
    {
        return OcupacionPorSesion(desde, hasta)
            .GroupBy(s => s.Sala)
            .Select(g => new OcupacionSala(
                g.Key,
                g.Count(),
                g.Sum(s => s.Aforo),
                g.Sum(s => Math.Min(s.Confirmadas, s.Aforo)),
                CalculosEstadisticas.OcupacionMedia(g.Select(s => (s.Confirmadas, s.Aforo)))))
            .OrderByDescending(s => s.OcupacionPorcentaje)
            .ThenBy(s => s.Sala)
            .ToList();
    }

    public List<AltasMes> AltasClientesPorMes(int anio)
    {
        Dictionary<int, int> porMes = _contexto.Clientes
            .AsNoTracking()
            .Where(c => c.FechaAlta.Year == anio)
            .GroupBy(c => c.FechaAlta.Month)
            .Select(g => new { Mes = g.Key, Total = g.Count() })
            .ToDictionary(x => x.Mes, x => x.Total);

        return CalculosEstadisticas.CompletarMeses(porMes);
    }
}
