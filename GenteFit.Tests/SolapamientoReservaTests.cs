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

public class SolapamientoReservaTests
{
    [Theory]
    [InlineData(EstadoReserva.CONFIRMADA)]
    [InlineData(EstadoReserva.EN_ESPERA)]
    public async Task Reservar_HorarioSolapado_RechazaReserva(
        EstadoReserva estadoInicial)
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

            // Crear 17 clientes.
            var clientes = new List<Cliente>();

            for (int i = 1; i <= 17; i++)
            {
                var usuario = new Usuario
                {
                    NombreUsuario = $"cliente_solape_{i}",
                    Email = $"solape{i}@gentefit.test",
                    HashContrasena = hash,
                    Rol = Rol.CLIENTE,
                    Activo = true
                };

                clientes.Add(new Cliente
                {
                    Dni = $"{95000000 + i}A",
                    Nombre = $"Cliente {i}",
                    Apellidos = "Prueba Solapamiento",
                    Email = usuario.Email,
                    Telefono = "600000000",
                    FechaNacimiento = new DateTime(1995, 1, 1),
                    FechaAlta = DateTime.Today,
                    Activo = true,
                    Usuario = usuario
                });
            }

            // Utilizaremos el cliente 17 para
            // comprobar las reglas de negocio.
            Cliente clientePrueba = clientes[16];

            // Crear dos monitores diferentes.
            var usuarioMonitor1 = new Usuario
            {
                NombreUsuario = "monitor_solape_1",
                Email = "monitor.solape1@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.MONITOR,
                Activo = true
            };

            var monitor1 = new MonitorDeportivo
            {
                Dni = "96000001A",
                Nombre = "Laura",
                Apellidos = "Pruebas",
                Email = usuarioMonitor1.Email,
                Telefono = "600000001",
                Especialidad = "Yoga",
                Activo = true,
                Usuario = usuarioMonitor1
            };

            var usuarioMonitor2 = new Usuario
            {
                NombreUsuario = "monitor_solape_2",
                Email = "monitor.solape2@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.MONITOR,
                Activo = true
            };

            var monitor2 = new MonitorDeportivo
            {
                Dni = "96000002B",
                Nombre = "Carlos",
                Apellidos = "Pruebas",
                Email = usuarioMonitor2.Email,
                Telefono = "600000002",
                Especialidad = "Pilates",
                Activo = true,
                Usuario = usuarioMonitor2
            };

            var sala1 = new Sala
            {
                Nombre = "Sala Solapamiento 1",
                Ubicacion = "Planta 1",
                Activa = true
            };

            var sala2 = new Sala
            {
                Nombre = "Sala Solapamiento 2",
                Ubicacion = "Planta 2",
                Activa = true
            };

            var actividad = new Actividad
            {
                Nombre = "Yoga Solapamiento",
                Descripcion = "Prueba de horarios",
                Intensidad = NivelIntensidad.BAJA,
                DuracionMinutos = 60,
                Activa = true
            };

            contexto.Clientes.AddRange(clientes);

            contexto.AddRange(
                monitor1, monitor2,
                sala1, sala2, actividad);

            await contexto.SaveChangesAsync();

            // Preparar las sesiones para dentro de 4 días.
            DateTime inicio = DateTime.Today
                .AddDays(4)
                .AddHours(10);

            // Sesión original: 10:00 - 11:00.
            var sesionOriginal = new Sesion
            {
                ActividadId = actividad.Id,
                SalaId = sala1.Id,
                MonitorId = monitor1.Id,
                FechaHoraInicio = inicio,
                FechaHoraFin = inicio.AddHours(1),
                AforoMaximo = 16,
                Anulada = false
            };

            // Sesión solapada: 10:30 - 11:30.
            var sesionSolapada = new Sesion
            {
                ActividadId = actividad.Id,
                SalaId = sala2.Id,
                MonitorId = monitor2.Id,
                FechaHoraInicio = inicio.AddMinutes(30),
                FechaHoraFin = inicio.AddMinutes(90),
                AforoMaximo = 16,
                Anulada = false
            };

            // Sesión consecutiva: 11:00 - 12:00.
            var sesionConsecutiva = new Sesion
            {
                ActividadId = actividad.Id,
                SalaId = sala1.Id,
                MonitorId = monitor1.Id,
                FechaHoraInicio = inicio.AddHours(1),
                FechaHoraFin = inicio.AddHours(2),
                AforoMaximo = 16,
                Anulada = false
            };

            contexto.Sesiones.AddRange(
                sesionOriginal,
                sesionSolapada,
                sesionConsecutiva);

            await contexto.SaveChangesAsync();

            // Si comprobamos EN_ESPERA, llenamos
            // previamente las 16 plazas.
            if (estadoInicial == EstadoReserva.EN_ESPERA)
            {
                for (int i = 0; i < 16; i++)
                {
                    contexto.Reservas.Add(new Reserva
                    {
                        ClienteId = clientes[i].Id,
                        SesionId = sesionOriginal.Id,
                        Estado = EstadoReserva.CONFIRMADA,
                        FechaHoraReserva =
                            DateTime.UtcNow.AddMinutes(-30)
                    });
                }

                await contexto.SaveChangesAsync();
            }

            var repositorio =
                new ReservaRepositorio(contexto);

            // PRUEBA 1: crear la primera reserva.
            var reservaInicial = await repositorio.CrearAsync(
                clientePrueba.Id,
                sesionOriginal.Id);

            Assert.Equal(
                estadoInicial,
                reservaInicial.Estado);

            // PRUEBA 2: intentar reservar otra
            // sesión con horario solapado.
            var excepcion =
                await Assert.ThrowsAsync<GenteFitException>(
                    () => repositorio.CrearAsync(
                        clientePrueba.Id,
                        sesionSolapada.Id));

            Assert.Contains(
                "horario",
                excepcion.Message,
                StringComparison.OrdinalIgnoreCase);

            // PRUEBA 3: las sesiones consecutivas
            // sí pueden reservarse.
            var reservaConsecutiva =
                await repositorio.CrearAsync(
                    clientePrueba.Id,
                    sesionConsecutiva.Id);

            Assert.Equal(
                EstadoReserva.CONFIRMADA,
                reservaConsecutiva.Estado);

            // Comprobar SQL Server con otro DbContext.
            await using var verificacion =
                new GenteFitDbContext(opciones);

            // No debe haberse creado una reserva
            // para la sesión solapada.
            bool existeSolapada = await verificacion.Reservas
                .AsNoTracking()
                .AnyAsync(r =>
                    r.ClienteId == clientePrueba.Id &&
                    r.SesionId == sesionSolapada.Id);

            Assert.False(existeSolapada);

            // Deben existir las dos reservas válidas.
            int reservasValidas = await verificacion.Reservas
                .AsNoTracking()
                .CountAsync(r =>
                    r.ClienteId == clientePrueba.Id &&
                    r.Estado != EstadoReserva.ANULADA);

            Assert.Equal(2, reservasValidas);

            // La reserva consecutiva debe estar guardada.
            bool consecutivaGuardada =
                await verificacion.Reservas
                    .AsNoTracking()
                    .AnyAsync(r =>
                        r.ClienteId == clientePrueba.Id &&
                        r.SesionId == sesionConsecutiva.Id &&
                        r.Estado == EstadoReserva.CONFIRMADA);

            Assert.True(consecutivaGuardada);
        }
        finally
        {
            // Eliminar únicamente la BD temporal.
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
