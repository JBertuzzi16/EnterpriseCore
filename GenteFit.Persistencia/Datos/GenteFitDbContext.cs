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

    // Creacion de las relaciones
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cliente>()
            .HasOne(c => c.Usuario)
            .WithOne(u => u.Cliente)
            .HasForeignKey<Cliente>(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MonitorDeportivo>()
            .HasOne(m => m.Usuario)
            .WithOne(u => u.Monitor)
            .HasForeignKey<MonitorDeportivo>(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Sesion>()
            .HasOne(s=>s.Sala)
            .WithMany(sala => sala.Sesiones)
            .HasForeignKey(s => s.SalaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Sesion>()
            .HasOne(s => s.Monitor)
            .WithMany(m => m.Sesiones)
            .HasForeignKey(s => s.MonitorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reserva>()
            .HasOne(s=>s.Cliente)
            .WithMany(c =>c.Reservas)
            .HasForeignKey(r => r.ClienteId)
             .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reserva>()
            .HasOne(r => r.Sesion)
            .WithMany(s => s.Reservas)
            .HasForeignKey(r => r.SesionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Sesion>()
             .HasOne(s => s.Actividad)
            .WithMany(a => a.Sesiones)
            .HasForeignKey(s => s.ActividadId)
            .OnDelete(DeleteBehavior.Restrict);

        // no puede haber dos clientes con el mismo DNI ni con el mismo email

        modelBuilder.Entity<Cliente>()
            .Property(c => c.Dni)
            .HasMaxLength(20)
            .IsRequired();

        modelBuilder.Entity<Cliente>()
            .Property(c => c.Email)
            .HasMaxLength(254)
            .IsRequired();

        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Dni)
            .IsUnique();

        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Email)
            .IsUnique();

        // evita que dos cuentas tengan el mismo nombre de usuario

        modelBuilder.Entity<Usuario>()
            .Property(u => u.NombreUsuario)
            .HasMaxLength(50)
            .IsRequired();

        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.NombreUsuario)
            .IsUnique();

        modelBuilder.Entity<Usuario>()
            .Property(u => u.Email)
            .HasMaxLength(254)
            .IsRequired();

        modelBuilder.Entity<Usuario>()
            .Property(u => u.HashContrasena)
            .HasMaxLength(512)
            .IsRequired();
        // Evita que un cliente tenga dos reservas activas para la misma sesion

        modelBuilder.Entity<Reserva>()
            .HasIndex(r => new { r.ClienteId, r.SesionId })
            .IsUnique()
            .HasFilter("[Estado] IN (0, 1)");

        modelBuilder.Entity<Sesion>()
         .ToTable(tabla =>
         {
            tabla.HasCheckConstraint(
            "CK_Sesion_Aforo",
            "[AforoMaximo] = 16");

            tabla.HasCheckConstraint(
            "CK_Sesion_Horario",
            "[FechaHoraFin] > [FechaHoraInicio]");
    });
    }

}
