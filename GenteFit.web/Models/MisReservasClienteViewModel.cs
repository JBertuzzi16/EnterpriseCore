using GenteFit.Persistencia.Repositorios;

namespace GenteFit.Web.Models;

public class MisReservasClienteViewModel
{
    public List<ReservaClienteResumen> Reservas { get; set; }
        = new();

    public string? MensajeError { get; set; }
}
