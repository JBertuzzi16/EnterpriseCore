using System;
using System.Collections.Generic;
using System.Text;

namespace GenteFit.Persistencia.Entidades;

public class Sesion
{
    public int Id { get; set; }

    public DateTime FechaHoraInicio { get; set; }

    public DateTime FechaHoraFin { get; set; }

    public int AforoMaximo { get; set; } = 16;

    public bool Anulada { get; set; } = false;

    public int ActividadId { get; set; }

    public Actividad Actividad { get; set; } = null!;

    public int SalaId { get; set; }

    public Sala Sala { get; set; } = null!;

    public int MonitorId { get; set; }

    public Monitor Monitor { get; set; } = null!;

    public List<Reserva> Reservas { get; set; }= new List<Reserva>();
}
