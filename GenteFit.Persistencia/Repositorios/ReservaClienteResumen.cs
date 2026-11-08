using GenteFit.Persistencia.Entidades;

namespace GenteFit.Persistencia.Repositorios;

public class ReservaClienteResumen
{
    public int Id { get; set; }
    public int SesionId { get; set; }

    public string NombreActividad { get; set; } = "";
    public string NombreSala { get; set; } = "";
    public string NombreMonitor { get; set; } = "";

    public DateTime FechaHoraInicio { get; set; }
    public DateTime FechaHoraFin { get; set; }

    public EstadoReserva Estado { get; set; }

    public bool SesionAnulada { get; set; }

    // Solo se calcula para reservas EN_ESPERA.
    public int? PosicionEspera { get; set; }
}
