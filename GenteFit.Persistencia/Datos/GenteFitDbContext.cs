using System;
using System.Collections.Generic;
using System.Text;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;
using MonitorDeportivo = GenteFit.Persistencia.Entidades.Monitor;

namespace GenteFit.Persistencia.Datos;

public class GenteFitDbContext:DbContext
{
    public GenteFitDbContext(DbContextOptions<GenteFitDbContext> options) : base(options)
    {   }

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<MonitorDeportivo> Monitores => Set<MonitorDeportivo>();

    public DbSet<Sala> Salas => Set<Sala>();

    public DbSet<Actividad> Actividades => Set<Actividad>();

    public DbSet<Sesion> Sesiones => Set<Sesion>();

    public DbSet<Reserva> Reservas => Set<Reserva>();

}
