
using System.Text.Json;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Persistencia.Seguridad;
using GenteFit.Persistencia.Excepciones;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

using MonitorDeportivo = GenteFit.Persistencia.Entidades.Monitor;

namespace GenteFit.Tests;

public class ReservaRepositorioTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reservar_ConPlazas_ReservaConfirmada(
    bool comprobarDuplicado)
    {
        // Crear una base de datos temporal.
        string cadenaOriginal = ObtenerCadenaConexion();

        var conexion = new SqlConnectionStringBuilder(
            cadenaOriginal);

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

            // Crear un usuario de prueba.
            var usuario = new Usuario
            {
                NombreUsuario = "cliente_reserva_test",
                Email = "reserva.test@gentefit.test",
                HashContrasena =
                    HashContrasena.Crear("Gente.Fit1"),
                Rol = Rol.CLIENTE,
                Activo = true
            };

            var cliente = new Cliente
            {
                Dni = "88888888A",
                Nombre = "Cliente",
                Apellidos = "Prueba Reserva",
                Email = usuario.Email,
                Telefono = "600000000",
                FechaNacimiento = new DateTime(1995, 1, 1),
                FechaAlta = DateTime.Today,
                Activo = true,
                Usuario = usuario
            };

            // Crear monitor, sala y actividad.
            var usuarioMonitor = new Usuario
            {
                NombreUsuario = "monitor_reserva_test",
                Email = "monitor.reserva@gentefit.test",
                HashContrasena =
                    HashContrasena.Crear("Gente.Fit1"),
                Rol = Rol.MONITOR,
                Activo = true
            };

            var monitor = new MonitorDeportivo
            {
                Dni = "88888889B",
                Nombre = "Laura",
                Apellidos = "Pruebas",
                Email = usuarioMonitor.Email,
                Telefono = "600000001",
                Especialidad = "Yoga",
                Activo = true,
                Usuario = usuarioMonitor
            };

            var sala = new Sala
            {
                Nombre = "Sala Test",
                Ubicacion = "Planta 1",
                Activa = true
            };

            var actividad = new Actividad
            {
                Nombre = "Yoga Test",
                Descripcion = "Actividad de prueba",
                Intensidad = NivelIntensidad.BAJA,
                DuracionMinutos = 60,
                Activa = true
            };

            contexto.AddRange(
                cliente, monitor, sala, actividad);

            await contexto.SaveChangesAsync();

            // Crear una sesión futura con 16 plazas.
            DateTime inicio = DateTime.Now.Date
                .AddDays(3)
                .AddHours(18);

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

            // ACT: realizar la reserva.
            var repositorio =
                new ReservaRepositorio(contexto);

            var reserva = await repositorio.CrearAsync(
                cliente.Id,
                sesion.Id);

            // ASSERT: comprobar el estado devuelto.
            Assert.Equal(
                EstadoReserva.CONFIRMADA,
                reserva.Estado);

            // Verificar los datos directamente en SQL Server
            // utilizando un DbContext independiente.
            await using var verificacion =
                new GenteFitDbContext(opciones);

            var guardada = await verificacion.Reservas
                .AsNoTracking()
                .SingleAsync(r => r.Id == reserva.Id);

            Assert.Equal(cliente.Id, guardada.ClienteId);
            Assert.Equal(sesion.Id, guardada.SesionId);
            Assert.Equal(
                EstadoReserva.CONFIRMADA,
                guardada.Estado);

            // Comprobar que quedan 15 plazas.
            var sesiones =
                new SesionRepositorio(verificacion);

            int? plazasLibres =
                await sesiones.ObtenerPlazasLibresAsync(
                    sesion.Id);

            Assert.Equal(15, plazasLibres);

            // SEGUNDA PRUEBA: reserva duplicada.
            if (comprobarDuplicado)
            {
                // El cliente intenta reservar otra vez
                // la misma sesión.
                var excepcion = await Assert.ThrowsAsync
                    <GenteFitException>(async () =>
                    {
                        await repositorio.CrearAsync(
                            cliente.Id,
                            sesion.Id);
                    });

                // Comprobar que se informa al usuario.
                Assert.Contains(
                    "reserva activa",
                    excepcion.Message,
                    StringComparison.OrdinalIgnoreCase);

                // Verificar que sigue existiendo
                // una sola reserva activa en SQL Server.
                int totalReservas = await verificacion.Reservas
                    .AsNoTracking()
                    .CountAsync(r =>
                        r.ClienteId == cliente.Id &&
                        r.SesionId == sesion.Id &&
                        r.Estado != EstadoReserva.ANULADA);

                Assert.Equal(1, totalReservas);
            }

        }
        finally
        {
            // Eliminar únicamente la base temporal
            // creada para esta prueba.
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
