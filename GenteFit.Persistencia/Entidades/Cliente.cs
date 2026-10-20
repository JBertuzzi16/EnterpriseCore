using System;
using System.Collections.Generic;
using System.Text;

namespace GenteFit.Persistencia.Entidades;

public class Cliente
{
    public int Id { get; set; }

    public string Dni { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Apellidos { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public DateTime FechaNacimiento { get; set; }

    public DateTime FechaAlta { get; set; }

    public bool Activo { get; set; } = true;

    public int UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public List <Reserva> Reservas { get; set; }= new List<Reserva>();
}
