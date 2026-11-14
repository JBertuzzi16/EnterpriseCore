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

public class AnulacionesNoPermitidasTests
{
    [Theory]
    [InlineData(false)] // Reserva ajena
    [InlineData(true)]  // Sesión pasada
    public async Task Anular_NoPermitida_MantieneReserva(
        bool sesionPasada)
    {
        // Crear una base de datos temporal.
        var conexion = new SqlConnectionStringBuilder(
            ObtenerCadenaConexion());

        conexion.InitialCatalog =
            "GenteFit_Test_" + Guid.NewGuid().ToString("N");

        var opciones =
            new DbContextOptionsBuilder<GenteFitDbContext>()
                .UseSqlServer(conexion.ConnectionString)
                .Options;

        bool baseCreada = false;

        try
        {
            await using var contexto =
                new GenteFitDbContext(opciones);

            await contexto.Database.EnsureCreatedAsync();
            baseCreada = true;

            string hash = HashContrasena.Crear("Gente.Fit1");

            // CLIENTE A: propietario de la reserva.
            var usuarioA = new Usuario
            {
                NombreUsuario = "cliente_anulacion_A",
                Email = "anulacion.a@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.CLIENTE,
                Activo = true
            };

            var clienteA = new Cliente
            {
                Dni = "97000001A",
                Nombre = "Cliente",
                Apellidos = "Propietario",
                Email = usuarioA.Email,
                Telefono = "600000001",
                FechaNacimiento = new DateTime(1995, 1, 1),
                FechaAlta = DateTime.Today,
                Activo = true,
                Usuario = usuarioA
            };

            // CLIENTE B: otro usuario del sistema.
            var usuarioB = new Usuario
            {
                NombreUsuario = "cliente_anulacion_B",
                Email = "anulacion.b@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.CLIENTE,
                Activo = true
            };

            var clienteB = new Cliente
            {
                Dni = "97000002B",
                Nombre = "Cliente",
                Apellidos = "Ajeno",
                Email = usuarioB.Email,
                Telefono = "600000002",
                FechaNacimiento = new DateTime(1996, 1, 1),
                FechaAlta = DateTime.Today,
                Activo = true,
                Usuario = usuarioB
            };

            // Crear monitor, sala y actividad.
            var usuarioMonitor = new Usuario
            {
                NombreUsuario = "monitor_anulacion_test",
                Email = "monitor.anulacion.test@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.MONITOR,
                Activo = true
            };

            var monitor = new MonitorDeportivo
            {
                Dni = "97000003C",
                Nombre = "Laura",
                Apellidos = "Pruebas",
                Email = usuarioMonitor.Email,
                Telefono = "600000003",
                Especialidad = "Yoga",
                Activo = true,
                Usuario = usuarioMonitor
            };

            var sala = new Sala
            {
                Nombre = "Sala Anulación",
                Ubicacion = "Planta 1",
                Activa = true
            };

            var actividad = new Actividad
            {
                Nombre = "Yoga Anulación",
                Descripcion = "Prueba de seguridad",
                Intensidad = NivelIntensidad.BAJA,
                DuracionMinutos = 60,
                Activa = true
            };

            contexto.AddRange(
                clienteA, clienteB,
                monitor, sala, actividad);

            await contexto.SaveChangesAsync();

            // Preparar la sesión.
            DateTime inicio = sesionPasada
                ? DateTime.Today.AddDays(-2).AddHours(18)
                : DateTime.Today.AddDays(3).AddHours(18);

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

            // Insertamos una reserva confirmada.
            // La insertamos directamente porque CrearAsync
            // rechaza correctamente las sesiones pasadas.
            var reserva = new Reserva
            {
                ClienteId = clienteA.Id,
                SesionId = sesion.Id,
                Estado = EstadoReserva.CONFIRMADA,
                FechaHoraReserva = inicio.AddDays(-1),
                FechaHoraAnulacion = null
            };

            contexto.Reservas.Add(reserva);
            await contexto.SaveChangesAsync();

            var repositorio =
                new ReservaRepositorio(contexto);

            // En la primera prueba intenta anular B.
            // En la segunda intenta anular A,
            // pero la sesión ya ha comenzado.
            int clienteQueAnula = sesionPasada
                ? clienteA.Id
                : clienteB.Id;

            // ACT: intentar una anulación no permitida.
            var excepcion =
                await Assert.ThrowsAsync<GenteFitException>(
                    () => repositorio.AnularAsync(
                        clienteQueAnula,
                        reserva.Id));

            // Comprobar que se informa del motivo.
            if (sesionPasada)
            {
                Assert.Contains(
                    "ya ha comenzado",
                    excepcion.Message,
                    StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.Contains(
                    "reserva tuya",
                    excepcion.Message,
                    StringComparison.OrdinalIgnoreCase);
            }

            // Comprobar los datos reales en SQL Server
            // con un DbContext independiente.
            await using var verificacion =
                new GenteFitDbContext(opciones);

            var reservaGuardada =
                await verificacion.Reservas
                    .AsNoTracking()
                    .SingleAsync(r => r.Id == reserva.Id);

            // La reserva debe seguir confirmada.
            Assert.Equal(
                EstadoReserva.CONFIRMADA,
                reservaGuardada.Estado);

            // No debe existir fecha de anulación.
            Assert.Null(
                reservaGuardada.FechaHoraAnulacion);

            // El propietario no debe haber cambiado.
            Assert.Equal(
                clienteA.Id,
                reservaGuardada.ClienteId);

            // Debe seguir existiendo una única reserva.
            int total = await verificacion.Reservas
                .AsNoTracking()
                .CountAsync(r => r.SesionId == sesion.Id);

            Assert.Equal(1, total);
        }
        finally
        {
            // Eliminar solamente la base temporal.
            if (baseCreada)
            {
                await using var limpieza =
                    new GenteFitDbContext(opciones);

                await limpieza.Database.EnsureDeletedAsync();
            }
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
