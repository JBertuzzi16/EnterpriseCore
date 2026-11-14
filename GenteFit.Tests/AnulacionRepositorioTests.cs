using System.Text.Json;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Persistencia.Seguridad;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

using MonitorDeportivo =
    GenteFit.Persistencia.Entidades.Monitor;

namespace GenteFit.Tests;

public class AnulacionRepositorioTests
{
    [Fact]
    public async Task Anular_Confirmada_PromocionaPrimeroDeLaCola()
    {
        // Crear una base de datos temporal independiente.
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

            // Crear 18 clientes activos.
            var clientes = new List<Cliente>();
            string hash = HashContrasena.Crear("Gente.Fit1");

            for (int i = 1; i <= 18; i++)
            {
                var usuario = new Usuario
                {
                    NombreUsuario = $"cliente_anulacion_{i}",
                    Email = $"anulacion{i}@gentefit.test",
                    HashContrasena = hash,
                    Rol = Rol.CLIENTE,
                    Activo = true
                };

                clientes.Add(new Cliente
                {
                    Dni = $"{91000000 + i}Z",
                    Nombre = $"Cliente {i}",
                    Apellidos = "Prueba Anulación",
                    Email = usuario.Email,
                    Telefono = "600000000",
                    FechaNacimiento = new DateTime(1995, 1, 1),
                    FechaAlta = DateTime.Today,
                    Activo = true,
                    Usuario = usuario
                });
            }

            // Crear monitor, sala y actividad.
            var usuarioMonitor = new Usuario
            {
                NombreUsuario = "monitor_anulacion",
                Email = "monitor.anulacion@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.MONITOR,
                Activo = true
            };

            var monitor = new MonitorDeportivo
            {
                Dni = "92000000Z",
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
                Nombre = "Sala Anulación Test",
                Ubicacion = "Planta 1",
                Activa = true
            };

            var actividad = new Actividad
            {
                Nombre = "Yoga Anulación Test",
                Descripcion = "Prueba de promoción automática",
                Intensidad = NivelIntensidad.BAJA,
                DuracionMinutos = 60,
                Activa = true
            };

            contexto.Clientes.AddRange(clientes);
            contexto.AddRange(monitor, sala, actividad);
            await contexto.SaveChangesAsync();

            // Crear una sesión futura.
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

            // Preparar 16 reservas confirmadas.
            // Insertamos los datos directamente para
            // centrarnos en probar AnularAsync().
            var confirmadas = new List<Reserva>();

            for (int i = 0; i < 16; i++)
            {
                confirmadas.Add(new Reserva
                {
                    ClienteId = clientes[i].Id,
                    SesionId = sesion.Id,
                    Estado = EstadoReserva.CONFIRMADA,
                    FechaHoraReserva =
                        DateTime.UtcNow.AddMinutes(-30)
                });
            }

            // Dos clientes entran en lista de espera.
            // Usamos fechas diferentes para comprobar
            // que se respeta el orden de entrada.
            var primeraEspera = new Reserva
            {
                ClienteId = clientes[16].Id,
                SesionId = sesion.Id,
                Estado = EstadoReserva.EN_ESPERA,
                FechaHoraReserva =
                    DateTime.UtcNow.AddMinutes(-5)
            };

            var segundaEspera = new Reserva
            {
                ClienteId = clientes[17].Id,
                SesionId = sesion.Id,
                Estado = EstadoReserva.EN_ESPERA,
                FechaHoraReserva =
                    DateTime.UtcNow.AddMinutes(-4)
            };

            contexto.Reservas.AddRange(confirmadas);
            contexto.Reservas.AddRange(
                primeraEspera, segundaEspera);

            await contexto.SaveChangesAsync();

            // ACT: anular la primera reserva confirmada.
            var repositorio =
                new ReservaRepositorio(contexto);

            var reservaOriginal = confirmadas[0];

            var resultado = await repositorio.AnularAsync(
                reservaOriginal.ClienteId,
                reservaOriginal.Id);

            // Comprobar que se ha producido una promoción.
            Assert.True(resultado.HayPromocion);

            Assert.Equal(
                (int?)primeraEspera.Id,
                resultado.ReservaPromocionadaId);

            Assert.Equal(
                (int?)clientes[16].Id,
                resultado.ClientePromocionadoId);

            // Verificar los datos guardados en SQL Server
            // utilizando otro DbContext independiente.
            await using var verificacion =
                new GenteFitDbContext(opciones);

            var anulada = await verificacion.Reservas
                .AsNoTracking()
                .SingleAsync(r => r.Id == reservaOriginal.Id);

            var promocionada = await verificacion.Reservas
                .AsNoTracking()
                .SingleAsync(r => r.Id == primeraEspera.Id);

            var segunda = await verificacion.Reservas
                .AsNoTracking()
                .SingleAsync(r => r.Id == segundaEspera.Id);

            // Reserva original anulada.
            Assert.Equal(
                EstadoReserva.ANULADA,
                anulada.Estado);

            Assert.NotNull(anulada.FechaHoraAnulacion);

            // Primera persona promocionada.
            Assert.Equal(
                EstadoReserva.CONFIRMADA,
                promocionada.Estado);

            // Segunda persona continúa esperando.
            Assert.Equal(
                EstadoReserva.EN_ESPERA,
                segunda.Estado);

            // Comprobar el aforo y la cola.
            int totalConfirmadas = await verificacion.Reservas
                .AsNoTracking()
                .CountAsync(r =>
                    r.SesionId == sesion.Id &&
                    r.Estado == EstadoReserva.CONFIRMADA);

            int totalEnEspera = await verificacion.Reservas
                .AsNoTracking()
                .CountAsync(r =>
                    r.SesionId == sesion.Id &&
                    r.Estado == EstadoReserva.EN_ESPERA);

            Assert.Equal(16, totalConfirmadas);
            Assert.Equal(1, totalEnEspera);

            // La segunda persona debe ocupar
            // ahora la posición 1 de la lista.
            var reservasCliente =
                await new ReservaRepositorio(verificacion)
                    .ObtenerReservasClienteAsync(
                        clientes[17].Id);

            var reservaCliente =
                Assert.Single(reservasCliente);

            Assert.Equal(
                (int?)1,
                reservaCliente.PosicionEspera);
        }
        finally
        {
            // Eliminar solamente la base temporal.
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
