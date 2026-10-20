using System;
using System.Collections.Generic;
using System.Text;

namespace GenteFit.Persistencia.Entidades;

    public class Sala
    {
        public int Id { get; set; }
        public String Nombre { get; set; } = string.Empty;
        public String Ubicacion {  get; set; } = string.Empty;
        public bool Activa { get; set; } = true;
        public List <Sesion> Sesiones { get; set; } = new List<Sesion>();

    }

