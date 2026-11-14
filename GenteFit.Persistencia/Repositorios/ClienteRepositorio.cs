using GenteFit.Persistencia.Datos;
using System;
using System.Collections.Generic;
using System.Text;
using GenteFit.Persistencia.Entidades;

namespace GenteFit.Persistencia.Repositorios;


public class ClienteRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public ClienteRepositorio (GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    public List <Cliente> ObtenerTodos()
    {
        return _contexto.Clientes
            .OrderBy(cliente => cliente.Apellidos)
            .ThenBy(cliente => cliente.Nombre)
            .ToList();
    }

    public Cliente? ObtenerPorId (int id)
    {
        return _contexto.Clientes.Find (id);
    }
}
