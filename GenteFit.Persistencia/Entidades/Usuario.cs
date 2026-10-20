using System;
using System.Collections.Generic;
using System.Text;

namespace GenteFit.Persistencia.Entidades;

public class Usuario
{
    public int Id { get; set; }

    public string NombreUsuario { get; set; } = string.Empty;

    public string HashContrasena { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public Rol Rol { get; set; } =
        global::GenteFit.Persistencia.Entidades.Rol.CLIENTE;

    public bool Activo { get; set; } = true;
    public Cliente? Cliente { get; set; }

    public Monitor? Monitor { get; set; }
}
