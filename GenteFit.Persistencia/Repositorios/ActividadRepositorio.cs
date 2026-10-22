using GenteFit.Persistencia.Datos;
using System;
using System.Collections.Generic;
using System.Text;

namespace GenteFit.Persistencia.Repositorios;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;

public class ActividadRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public ActividadRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }
    public List<Actividad> ObtenerTodas()
    {
        return _contexto.Actividades
            .OrderBy(ac=>ac.Nombre)
            .ToList(); 
            }

    public Actividad? ObtenerPorId(int id)
    {
        return _contexto.Actividades.Find(id);
    }

    public  Actividad Crear(Actividad actividad)
    {
        ArgumentNullException.ThrowIfNull(actividad);

        if (string.IsNullOrWhiteSpace(actividad.Nombre))
        {
            throw new ArgumentException(
                "El nombre de la actividad es obligatorio.");
        }
        if (string.IsNullOrWhiteSpace(actividad.Descripcion))
        {
            throw new ArgumentException(
                "La descripción es obligatoria.");
        }
        if (actividad.DuracionMinutos <= 0) 
        {
            throw new ArgumentException(
                "La duración debe ser mayor que cero");       
        }
        if (!Enum.IsDefined(typeof(NivelIntensidad), actividad.Intensidad))
        {
            throw new ArgumentException(
                "El nivel de intensidad no es válido");
        }

        _contexto.Actividades.Add(actividad);
        _contexto.SaveChanges();

        return actividad;
    }

    public bool Actualizar (int id, string nombre, string descripcion, int duracionMinutos, NivelIntensidad intensidad )
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(descripcion))
            throw new ArgumentException("La descripcion es obligatoria.");
        if (duracionMinutos <= 0)
            throw new ArgumentException("La duración debe ser mayor que 0.");
        if (!Enum.IsDefined(typeof(NivelIntensidad), intensidad))
            throw new ArgumentException("El nivel de intensidad no es válido.");

        Actividad? actividad = ObtenerPorId(id);

        if (actividad == null)
            return false;
        actividad.Nombre = nombre.Trim();
        actividad.Descripcion = descripcion.Trim();
        actividad.DuracionMinutos = duracionMinutos;
        actividad.Intensidad = intensidad;

        _contexto.SaveChanges();

        return true;
    }

    public bool Desactivar(int id)
    {
        Actividad? actividad = ObtenerPorId(id);

        if (actividad == null)
            return false;

        actividad.Activa = false;
        _contexto.SaveChanges();

        return true;
    }

    public bool Activar(int id)
    {
        Actividad? actividad = ObtenerPorId(id);

        if (actividad == null)
            return false;

        actividad.Activa = true;
        _contexto.SaveChanges();

        return true;
    }

}

