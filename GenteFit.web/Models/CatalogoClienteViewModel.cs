using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;

namespace GenteFit.Web.Models;

public class CatalogoClienteViewModel
{
    public string? Nombre { get; set; }

    public DateTime? Fecha { get; set; }

    public NivelIntensidad? Intensidad { get; set; }

    public List<SesionResumen> Sesiones { get; set; }
        = new();

    // Clave: Id de la sesión.
    // Valor: estado de la reserva del cliente.
    public Dictionary<int, EstadoReserva> EstadosCliente
    { get; set; } = new();

    public string? MensajeError { get; set; }
}
