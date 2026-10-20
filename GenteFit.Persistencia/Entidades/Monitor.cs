using System;
using System.Collections.Generic;
using System.Text;

namespace GenteFit.Persistencia.Entidades;

public class Monitor
{
    public int Id { get; set; }

    public string Dni { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Apellidos { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public string Especialidad { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public int UsuarioId { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public List<Sesion> Sesiones { get; set; } = new List<Sesion>();

}
