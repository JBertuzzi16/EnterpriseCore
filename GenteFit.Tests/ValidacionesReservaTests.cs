using System;
using System.Text.Json;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Persistencia.Seguridad;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

using MonitorDeportivo =
    GenteFit.Persistencia.Entidades.Monitor;

namespace GenteFit.Tests;

public class ValidacionesReservaTests
{
    [Theory]
    [InlineData(false)] // Sesión pasada
    [InlineData(true)]  // Cliente inactivo
    public async Task Reservar_DatosInvalidos_RechazaReserva(
        bool clienteInactivo)
    {
        // Crear una BD temporal para cada prueba.
        var conexion = new SqlConnectionStringBuilder(
            ObtenerCadenaConexion());

        conexion.InitialCatalog =
            "GenteFit_Test_" + Guid.NewGuid().ToString("N");

        var opciones =
            new DbContextOptionsBuilder<GenteFitDbContext>()
                .UseSqlServer(conexion.ConnectionString)
                .Options;

        try
        {
            await using var contexto =
                new GenteFitDbContext(opciones);

            await contexto.Database.EnsureCreatedAsync();

            string hash = HashContrasena.Crear("Gente.Fit1");

            // Crear un usuario y su cliente.
            var usuario = new Usuario
            {
                NombreUsuario = "cliente_validacion",
                Email = "validacion@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.CLIENTE,
                Activo = true
            };

            var cliente = new Cliente
            {
                Dni = "93000001A",
                Nombre = "Cliente",
                Apellidos = "Validación",
                Email = usuario.Email,
                Telefono = "600000001",
                FechaNacimiento = new DateTime(1995, 1, 1),
                FechaAlta = DateTime.Today,
                Activo = !clienteInactivo,
                Usuario = usuario
            };

            // Crear el monitor.
            var usuarioMonitor = new Usuario
            {
                NombreUsuario = "monitor_validacion",
                Email = "monitor.validacion@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.MONITOR,
                Activo = true
            };

            var monitor = new MonitorDeportivo
            {
                Dni = "93000002B",
                Nombre = "Laura",
                Apellidos = "Pruebas",
                Email = usuarioMonitor.Email,
                Telefono = "600000002",
                Especialidad = "Yoga",
                Activo = true,
                Usuario = usuarioMonitor
            };

            var sala = new Sala
            {
                Nombre = "Sala Validación",
                Ubicacion = "Planta 1",
                Activa = true
            };

            var actividad = new Actividad
            {
                Nombre = "Yoga Validación",
                Descripcion = "Prueba de reglas de negocio",
                Intensidad = NivelIntensidad.BAJA,
                DuracionMinutos = 60,
                Activa = true
            };

            contexto.AddRange(
                cliente, monitor, sala, actividad);

            await contexto.SaveChangesAsync();

            // Si el cliente está inactivo,
            // creamos una sesión futura.
            // Si está activo, creamos una pasada.
            DateTime inicio = clienteInactivo
                ? DateTime.Today.AddDays(3).AddHours(18)
                : DateTime.Today.AddDays(-2).AddHours(18);

            var sesion = new Sesion
            {
                ActividadId = actividad.Id,
                SalaId = sala.Id,
                MonitorId = monitor.Id,
                FechaHoraInicio = inicio,
                FechaHoraFin = inicio.AddHours(1),
                AforoMaximo = 16,
                Anulada = false
            };

            contexto.Sesiones.Add(sesion);
            await contexto.SaveChangesAsync();

            // ACT: intentar realizar la reserva.
            var repositorio = new ReservaRepositorio(contexto);

            var excepcion =
                await Assert.ThrowsAsync<GenteFitException>(
                    () => repositorio.CrearAsync(
                        cliente.Id,
                        sesion.Id));

            // Comprobar el motivo del rechazo.
            if (clienteInactivo)
            {
                Assert.Contains(
                    "dado de baja",
                    excepcion.Message,
                    StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.Contains(
                    "ya ha comenzado",
                    excepcion.Message,
                    StringComparison.OrdinalIgnoreCase);
            }

            // Verificar que SQL Server no ha guardado
            // ninguna reserva inválida.
            await using var verificacion =
                new GenteFitDbContext(opciones);

            bool existeReserva = await verificacion.Reservas
                .AsNoTracking()
                .AnyAsync(r =>
                    r.ClienteId == cliente.Id &&
                    r.SesionId == sesion.Id);

            Assert.False(existeReserva);
        }
        finally
        {
            // Limpiar únicamente la base temporal.
            await using var limpieza =
                new GenteFitDbContext(opciones);

            await limpieza.Database.EnsureDeletedAsync();
        }
    }

    private static string ObtenerCadenaConexion()
    {
        DirectoryInfo? carpeta =
            new DirectoryInfo(AppContext.BaseDirectory);

        while (carpeta != null)
        {
            string ruta = Path.Combine(
                carpeta.FullName,
                "GenteFit.web",
                "appsettings.json");

            if (File.Exists(ruta))
            {
                using var documento = JsonDocument.Parse(
                    File.ReadAllText(ruta),
                    new JsonDocumentOptions
                    {
                        CommentHandling =
                            JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });

                return documento.RootElement
                    .GetProperty("ConnectionStrings")
                    .GetProperty("GenteFit")
                    .GetString()
                    ?? throw new Exception(
                        "La cadena de conexión está vacía.");
            }

            carpeta = carpeta.Parent;
        }

        throw new FileNotFoundException(
            "No se encontró appsettings.json.");
    }
}
