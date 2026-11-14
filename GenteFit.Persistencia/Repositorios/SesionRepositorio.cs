
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;

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
}
