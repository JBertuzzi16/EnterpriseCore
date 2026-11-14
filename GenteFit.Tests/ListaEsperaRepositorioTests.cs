
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

public class ListaEsperaRepositorioTests
{
    [Fact]
    public async Task Reservar_SesionCompleta_EntraEnListaEspera()
    {
        // 1. Crear una base de datos temporal.
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

            // 2. Crear 17 clientes activos.
            var clientes = new List<Cliente>();

            // Hash válido para las cuentas de prueba.
            string hash = HashContrasena.Crear("Gente.Fit1");

            for (int i = 1; i <= 17; i++)
            {
                var usuario = new Usuario
                {
                    NombreUsuario = $"cliente_espera_{i}",
                    Email = $"cliente.espera{i}@gentefit.test",
                    HashContrasena = hash,
                    Rol = Rol.CLIENTE,
                    Activo = true
                };

                var cliente = new Cliente
                {
                    Dni = $"{90000000 + i}Z",
                    Nombre = $"Cliente {i}",
                    Apellidos = "Prueba Lista Espera",
                    Email = usuario.Email,
                    Telefono = "600000000",
                    FechaNacimiento = new DateTime(1995, 1, 1),
                    FechaAlta = DateTime.Today,
                    Activo = true,
                    Usuario = usuario
                };

                clientes.Add(cliente);
            }

            contexto.Clientes.AddRange(clientes);

            // 3. Crear monitor, sala y actividad.
            var usuarioMonitor = new Usuario
            {
                NombreUsuario = "monitor_espera_test",
                Email = "monitor.espera@gentefit.test",
                HashContrasena = hash,
                Rol = Rol.MONITOR,
                Activo = true
            };

            var monitor = new MonitorDeportivo
            {
                Dni = "99000000Z",
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
                Nombre = "Sala Lista Espera",
                Ubicacion = "Planta 1",
                Activa = true
            };

            var actividad = new Actividad
            {
                Nombre = "Yoga Lista Espera",
                Descripcion = "Prueba de aforo completo",
                Intensidad = NivelIntensidad.BAJA,
                DuracionMinutos = 60,
                Activa = true
            };

            contexto.AddRange(monitor, sala, actividad);
            await contexto.SaveChangesAsync();

            // 4. Crear una sesión futura con 16 plazas.
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

            // 5. Reservar las primeras 16 plazas.
            var repositorio = new ReservaRepositorio(contexto);

            for (int i = 0; i < 16; i++)
            {
                var reserva = await repositorio.CrearAsync(
                    clientes[i].Id,
                    sesion.Id);

                Assert.Equal(
                    EstadoReserva.CONFIRMADA,
                    reserva.Estado);
            }

            // 6. El cliente 17 intenta reservar.
            var reservaEspera = await repositorio.CrearAsync(
                clientes[16].Id,
                sesion.Id);

            Assert.Equal(
                EstadoReserva.EN_ESPERA,
                reservaEspera.Estado);

            // 7. Comprobar los registros de SQL Server
            // con un DbContext independiente.
            await using var verificacion =
                new GenteFitDbContext(opciones);

            int confirmadas = await verificacion.Reservas
                .AsNoTracking()
                .CountAsync(r =>
                    r.SesionId == sesion.Id &&
                    r.Estado == EstadoReserva.CONFIRMADA);

            int enEspera = await verificacion.Reservas
                .AsNoTracking()
                .CountAsync(r =>
                    r.SesionId == sesion.Id &&
                    r.Estado == EstadoReserva.EN_ESPERA);

            Assert.Equal(16, confirmadas);
            Assert.Equal(1, enEspera);

            // 8. Comprobar que no quedan plazas.
            var sesiones =
                new SesionRepositorio(verificacion);

            int? plazasLibres =
                await sesiones.ObtenerPlazasLibresAsync(
                    sesion.Id);

            Assert.Equal(0, plazasLibres);

            // 9. Comprobar la posición de espera.
            var reservasCliente =
                await new ReservaRepositorio(verificacion)
                    .ObtenerReservasClienteAsync(
                        clientes[16].Id);

            var reservaCliente = Assert.Single(
                reservasCliente);

            Assert.Equal(
                EstadoReserva.EN_ESPERA,
                reservaCliente.Estado);

            Assert.Equal(
                1,
                reservaCliente.PosicionEspera);
        }
        finally
        {
            // 10. Eliminar únicamente la BD temporal.
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
