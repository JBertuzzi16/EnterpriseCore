using System;
using System.Collections.Generic;
using System.Text;

namespace GenteFit.Persistencia.Entidades;

public class Actividad
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public NivelIntensidad Intensidad { get; set; }

    public int DuracionMinutos { get; set; }

    public bool Activa { get; set; } = true;

    public List <Sesion> Sesiones { get; set; } = new List<Sesion>();
}
