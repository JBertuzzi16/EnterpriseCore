using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace GenteFit.Persistencia.Entidades;

public class Reserva
{
    public int Id { get; set; }

    public DateTime FechaHoraReserva { get; set; }

    public EstadoReserva Estado { get; set; } =
        EstadoReserva.EN_ESPERA;

    public DateTime? FechaHoraAnulacion { get; set; }

    public int ClienteId { get; set; }

    public Cliente Cliente { get; set; } = null!;

    public int SesionId { get; set; }

    public Sesion Sesion { get; set; } = null!;
}

