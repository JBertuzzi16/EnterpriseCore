using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using System;
using System.Collections.Generic;
using System.Text;

using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;

namespace GenteFit.Persistencia.Repositorios;

public class SalaRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public SalaRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    public Sala Crear(Sala sala)
    {
        ArgumentNullException.ThrowIfNull(sala);

        if (string.IsNullOrWhiteSpace(sala.Nombre))
        {
            throw new ArgumentException(
                "El nombre de la sala es obligatorio.");
        }

        _contexto.Salas.Add(sala);
        _contexto.SaveChanges();

        return sala;
    }

    public List <Sala> ObtenerTodas()
    {
        return _contexto.Salas
            .OrderBy(sala => sala.Nombre)
            .ToList();
    }

    //public List<Sala> ObtenerActivas()
    //{
    //    return _contexto.Salas
    //        .OrderBy(sala => sala.Nombre)
    //        .Where(sala => sala.Activa)
    //        .ToList();
    //}
    public Sala? ObtenerPorId(int id)
    {
        return _contexto.Salas.Find(id);
    }
    public bool Actualizar(int id, String nombre, string ubicacion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(ubicacion))
            throw new ArgumentException("La ubicación es obligatoria.");

        Sala? sala = ObtenerPorId(id);

        if (sala == null)
            return false;
        sala.Nombre = nombre.Trim();
        sala.Ubicacion = ubicacion.Trim();

        _contexto.SaveChanges();

        return true;
    }

    public bool Desactivar(int id)
    {
        Sala? sala = ObtenerPorId(id);

        if (sala == null)
            return false;

        sala.Activa = false;

        _contexto.SaveChanges();

        return true;
    }
    public bool Activar(int id)
    {
        Sala? sala = ObtenerPorId(id);

        if (sala == null)
            return false;

        sala.Activa = true;
        _contexto.SaveChanges();

        return true;
    }
}

