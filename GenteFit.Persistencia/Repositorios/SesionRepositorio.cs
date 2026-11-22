
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;
using System.Data;


namespace GenteFit.Persistencia.Repositorios;

// Datos necesarios para mostrar una sesión en el catálogo.
public class SesionResumen
{
    public int Id { get; set; }
    public string NombreActividad { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public NivelIntensidad Intensidad { get; set; }
    public int DuracionMinutos { get; set; }

    public DateTime FechaHoraInicio { get; set; }
    public DateTime FechaHoraFin { get; set; }

    public string NombreSala { get; set; } = "";
    public string NombreMonitor { get; set; } = "";

    public int AforoMaximo { get; set; }
    public int PlazasOcupadas { get; set; }

    // Las plazas libres se calculan, no se guardan en BD.
    public int PlazasLibres =>
        Math.Max(0, AforoMaximo - PlazasOcupadas);

    public bool Completa => PlazasLibres == 0;
}

public class SesionRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public SesionRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    public List<Actividad> ObtenerActividadesActivas()
    {
        return _contexto.Actividades
            .AsNoTracking()
            .Where(a => a.Activa)
            .OrderBy(a => a.Nombre)
            .ToList();
    }

    public List<Sala> ObtenerSalasActivas()
    {
        return _contexto.Salas
            .AsNoTracking()
            .Where(a=>a.Activa)
            .OrderBy(a =>a.Nombre)
            .ToList() ;
    }

    public List<GenteFit.Persistencia.Entidades.Monitor> ObtenerMonitoresActivos()
    {
        return _contexto.Monitores
            .AsNoTracking()
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .ToList();
    }

    // Consulta las próximas sesiones disponibles para el cliente.
    public Task<List<SesionResumen>> ObtenerProximasAsync(
        string? nombre = null,
        DateTime? fecha = null,
        NivelIntensidad? intensidad = null,
        CancellationToken cancelacion = default)
    {
        DateTime ahora = DateTime.Now;

        var consulta = _contexto.Sesiones
            .AsNoTracking()
            .Where(s =>
                !s.Anulada &&
                s.FechaHoraInicio > ahora &&
                s.Actividad.Activa &&
                s.Sala.Activa &&
                s.Monitor.Activo);

        // Filtrar por nombre de actividad.
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            string texto = nombre.Trim();

            consulta = consulta.Where(s =>
                s.Actividad.Nombre.Contains(texto));
        }

        // Filtrar por día, independientemente de la hora.
        if (fecha.HasValue)
        {
            DateTime inicioDia = fecha.Value.Date;
            DateTime finDia = inicioDia.AddDays(1);

            consulta = consulta.Where(s =>
                s.FechaHoraInicio >= inicioDia &&
                s.FechaHoraInicio < finDia);
        }

        // Filtrar por intensidad.
        if (intensidad.HasValue)
        {
            if (!Enum.IsDefined(intensidad.Value))
                throw new ArgumentException(
                    "El nivel de intensidad no es válido.");

            consulta = consulta.Where(s =>
                s.Actividad.Intensidad == intensidad.Value);
        }

        // Obtenemos los datos para las tarjetas del catálogo.
        return consulta
            .OrderBy(s => s.FechaHoraInicio)
            .ThenBy(s => s.Actividad.Nombre)
            .Select(s => new SesionResumen
            {
                Id = s.Id,
                NombreActividad = s.Actividad.Nombre,
                Descripcion = s.Actividad.Descripcion,
                Intensidad = s.Actividad.Intensidad,
                DuracionMinutos = s.Actividad.DuracionMinutos,

                FechaHoraInicio = s.FechaHoraInicio,
                FechaHoraFin = s.FechaHoraFin,

                NombreSala = s.Sala.Nombre,
                NombreMonitor =
                    s.Monitor.Nombre + " " + s.Monitor.Apellidos,

                AforoMaximo = s.AforoMaximo,

                PlazasOcupadas = s.Reservas.Count(r =>
                    r.Estado == EstadoReserva.CONFIRMADA)
            })
            .ToListAsync(cancelacion);
    }

    // Consulta las plazas libres de una sesión concreta.
    // Devuelve null si la sesión no existe.
    public Task<int?> ObtenerPlazasLibresAsync(
        int sesionId,
        CancellationToken cancelacion = default)
    {
        return _contexto.Sesiones
            .AsNoTracking()
            .Where(s => s.Id == sesionId)
            .Select(s => (int?)(
                s.AforoMaximo -
                s.Reservas.Count(r =>
                    r.Estado == EstadoReserva.CONFIRMADA)))
            .FirstOrDefaultAsync(cancelacion);
    }

    public Sesion Crear (Sesion sesion)
    {
        ArgumentNullException.ThrowIfNull(sesion);

        if (sesion.FechaHoraInicio <= DateTime.Now)
            throw new ArgumentException(
                "La sesión debe empezar en una fecha futura.");
        if (sesion.FechaHoraFin <= sesion.FechaHoraInicio)
            throw new ArgumentException(
                "La hora de fin debe ser posterior al inicio.");

        using var transaccion = _contexto.Database.BeginTransaction(
            IsolationLevel.Serializable);

        if(!_contexto.Actividades.Any(a=>
        a.Id == sesion.ActividadId && a.Activa))
        {
            throw new ArgumentException(
                "Selecciona una actividad activa.");
        }
        if(!_contexto.Salas.Any(s=>
        s.Id == sesion.SalaId && s.Activa))
        {
            throw new ArgumentException(
                "Selecciona una sala activa.");
        }
        if(!_contexto.Monitores.Any(m=>
        m.Id == sesion.MonitorId && m.Activo))
        {
            throw new ArgumentException(
                "Selecciona un monitor activo.");
        }

        bool existeSolapamiento = _contexto.Sesiones.Any(s =>
       !s.Anulada &&
       s.FechaHoraInicio < sesion.FechaHoraFin &&
       s.FechaHoraFin > sesion.FechaHoraInicio &&
       (s.SalaId == sesion.SalaId ||
        s.MonitorId == sesion.MonitorId));

        if (existeSolapamiento)
            throw new ArgumentException(
                "La sala o el monitor ya están ocupados en ese horario.");

        sesion.AforoMaximo = 16;
        sesion.Anulada = false;

        _contexto.Sesiones.Add(sesion);
        _contexto.SaveChanges();

        transaccion.Commit();

        return sesion;
    }
    public List<Sesion> ObtenerTodas()
    {
        return _contexto.Sesiones
            .AsNoTracking()
            .Include(s => s.Actividad)
            .Include(s => s.Sala)
            .Include(s => s.Monitor)
            .OrderBy(s => s.FechaHoraInicio)
            .ThenBy(s => s.Id)
            .ToList();
    }
    public Sesion? ObtenerPorId(int id)
    {
        return _contexto.Sesiones
            .Include(s => s.Actividad)
            .SingleOrDefault(s => s.Id == id);
    }
    public bool Actualizar(Sesion datos)
    {
        using var transaccion = _contexto.Database.BeginTransaction(
            IsolationLevel.Serializable);

        var sesion = _contexto.Sesiones.Find(datos.Id);

        if (sesion == null)
            return false;

        if (sesion.Anulada || sesion.FechaHoraInicio <= DateTime.Now)
            throw new ArgumentException(
                "Solo se pueden editar sesiones futuras no anuladas.");

        if (datos.FechaHoraInicio <= DateTime.Now ||
            datos.FechaHoraFin <= datos.FechaHoraInicio)
        {
            throw new ArgumentException(
                "El inicio debe ser futuro y el fin posterior al inicio.");
        }

        bool tieneReservas = _contexto.Reservas.Any(r =>
            r.SesionId == datos.Id &&
            (r.Estado == EstadoReserva.CONFIRMADA ||
             r.Estado == EstadoReserva.EN_ESPERA));

        if (tieneReservas)
            throw new ArgumentException(
                "No se puede editar una sesión con reservas activas.");

        if (!_contexto.Actividades.Any(a =>
            a.Id == datos.ActividadId && a.Activa))
            throw new ArgumentException("Selecciona una actividad activa.");

        if (!_contexto.Salas.Any(s =>
            s.Id == datos.SalaId && s.Activa))
            throw new ArgumentException("Selecciona una sala activa.");

        if (!_contexto.Monitores.Any(m =>
            m.Id == datos.MonitorId && m.Activo))
            throw new ArgumentException("Selecciona un monitor activo.");

        bool existeSolapamiento = _contexto.Sesiones.Any(s =>
            s.Id != datos.Id &&
            !s.Anulada &&
            s.FechaHoraInicio < datos.FechaHoraFin &&
            s.FechaHoraFin > datos.FechaHoraInicio &&
            (s.SalaId == datos.SalaId ||
             s.MonitorId == datos.MonitorId));

        if (existeSolapamiento)
            throw new ArgumentException(
                "La sala o el monitor ya están ocupados en ese horario.");

        sesion.ActividadId = datos.ActividadId;
        sesion.SalaId = datos.SalaId;
        sesion.MonitorId = datos.MonitorId;
        sesion.FechaHoraInicio = datos.FechaHoraInicio;
        sesion.FechaHoraFin = datos.FechaHoraFin;

        _contexto.SaveChanges();
        transaccion.Commit();

        return true;
    }

    public bool Cancelar(int id)
    {
        using var transaccion = _contexto.Database.BeginTransaction(
            IsolationLevel.Serializable);

        var sesion = _contexto.Sesiones.Find(id);

        if (sesion == null)
            return false;

        if (sesion.Anulada)
            return true;

        if (sesion.FechaHoraInicio <= DateTime.Now)
            throw new ArgumentException(
                "No se puede cancelar una sesión que ya ha comenzado.");

        var reservas = _contexto.Reservas
            .Where(r => r.SesionId == id &&
                (r.Estado == EstadoReserva.CONFIRMADA ||
                 r.Estado == EstadoReserva.EN_ESPERA))
            .ToList();

        var fechaAnulacion = DateTime.UtcNow;

        foreach (var reserva in reservas)
        {
            reserva.Estado = EstadoReserva.ANULADA;
            reserva.FechaHoraAnulacion = fechaAnulacion;
        }

        sesion.Anulada = true;

        _contexto.SaveChanges();
        transaccion.Commit();

        return true;
    }

    public List<Reserva> ObtenerReservasConfirmadas(int sesionId)
    {
        return _contexto.Reservas
            .AsNoTracking()
            .Include(r => r.Cliente)
            .Where(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA)
            .OrderBy(r => r.Cliente.Apellidos)
            .ThenBy(r => r.Cliente.Nombre)
            .ToList();
    }

    public bool GuardarAsistencia(
    int sesionId,
    IReadOnlyDictionary<int, bool?> asistencias)
    {
        using var transaccion = _contexto.Database.BeginTransaction(
            IsolationLevel.Serializable);

        var sesion = _contexto.Sesiones.Find(sesionId);

        if (sesion == null)
            return false;

        if (sesion.Anulada)
            throw new ArgumentException(
                "No se puede registrar asistencia en una sesión anulada.");

        if (sesion.FechaHoraInicio > DateTime.Now)
            throw new ArgumentException(
                "La sesión todavía no ha comenzado.");

        var reservas = _contexto.Reservas
            .Where(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA)
            .ToList();

        if (reservas.Count != asistencias.Count ||
            reservas.Any(r => !asistencias.ContainsKey(r.Id)))
        {
            throw new ArgumentException(
                "La lista de participantes ha cambiado. Recarga la página.");
        }

        foreach (var reserva in reservas)
        {
            reserva.Asistio = asistencias[reserva.Id];
        }

        _contexto.SaveChanges();
        transaccion.Commit();

        return true;
    }


}
