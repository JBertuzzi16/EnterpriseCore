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

public class ConcurrenciaReservasTests
{
    [Fact]
    public async Task Reservar_UltimaPlaza_RespetaAforo()
    {
        // 1. Crear una BD temporal en SQL Server.
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
            int sesionId;
            int cliente16Id;
            int cliente17Id;

            // 2. Preparar los datos de prueba.
            await using (var contexto =
                new GenteFitDbContext(opciones))
            {
                await contexto.Database.EnsureCreatedAsync();
                baseCreada = true;

                string hash =
                    HashContrasena.Crear("Gente.Fit1");

                var clientes = new List<Cliente>();

                // Crear 17 clientes activos.
                for (int i = 1; i <= 17; i++)
                {
                    var usuario = new Usuario
                    {
                        NombreUsuario =
                            $"cliente_concurrencia_{i}",
                        Email =
                            $"concurrencia{i}@gentefit.test",
                        HashContrasena = hash,
                        Rol = Rol.CLIENTE,
                        Activo = true
                    };

                    clientes.Add(new Cliente
                    {
                        Dni = $"{98000000 + i}A",
                        Nombre = $"Cliente {i}",
                        Apellidos = "Prueba Concurrencia",
                        Email = usuario.Email,
                        Telefono = "600000000",
                        FechaNacimiento =
                            new DateTime(1995, 1, 1),
                        FechaAlta = DateTime.Today,
                        Activo = true,
                        Usuario = usuario
                    });
                }

                // Crear monitor, sala y actividad.
                var usuarioMonitor = new Usuario
                {
                    NombreUsuario = "monitor_concurrencia",
                    Email =
                        "monitor.concurrencia@gentefit.test",
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
                    Nombre = "Sala Concurrencia",
                    Ubicacion = "Planta 1",
                    Activa = true
                };

                var actividad = new Actividad
                {
                    Nombre = "Yoga Concurrencia",
                    Descripcion =
                        "Prueba de última plaza disponible",
                    Intensidad = NivelIntensidad.BAJA,
                    DuracionMinutos = 60,
                    Activa = true
                };

                contexto.Clientes.AddRange(clientes);

                contexto.AddRange(
                    monitor, sala, actividad);

                await contexto.SaveChangesAsync();

                // Crear una sesión futura con 16 plazas.
                DateTime inicio = DateTime.Today
                    .AddDays(5)
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

                // Ocupar 15 de las 16 plazas.
                for (int i = 0; i < 15; i++)
                {
                    contexto.Reservas.Add(new Reserva
                    {
                        ClienteId = clientes[i].Id,
                        SesionId = sesion.Id,
                        Estado = EstadoReserva.CONFIRMADA,
                        FechaHoraReserva =
                            DateTime.UtcNow.AddMinutes(-30)
                    });
                }

                await contexto.SaveChangesAsync();

                // Guardar solo los identificadores
                // necesarios para las tareas concurrentes.
                sesionId = sesion.Id;
                cliente16Id = clientes[15].Id;
                cliente17Id = clientes[16].Id;
            }

            // 3. Preparar la salida simultánea.
            //
            // Las dos tareas esperan hasta que
            // ambas estén listas para reservar.
            var salida =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

            int tareasPreparadas = 0;

            async Task<EstadoReserva> IntentarReservarAsync(
                int clienteId)
            {
                // Cada cliente utiliza su propio
                // DbContext y conexión a SQL Server.
                await using var contextoIndependiente =
                    new GenteFitDbContext(opciones);

                var repositorio =
                    new ReservaRepositorio(
                        contextoIndependiente);

                // Avisar de que esta tarea está lista.
                if (Interlocked.Increment(
                    ref tareasPreparadas) == 2)
                {
                    salida.TrySetResult(true);
                }

                // Esperar a la otra tarea.
                await salida.Task.WaitAsync(
                    TimeSpan.FromSeconds(15));

                // Ambas tareas intentan reservar
                // después de liberar la espera.
                var reserva = await repositorio.CrearAsync(
                    clienteId,
                    sesionId);

                return reserva.Estado;
            }

            // 4. Lanzar las dos reservas concurrentes.
            var tarea16 = Task.Run(
                () => IntentarReservarAsync(cliente16Id));

            var tarea17 = Task.Run(
                () => IntentarReservarAsync(cliente17Id));

            var resultados = await Task.WhenAll(
                tarea16,
                tarea17);

            // 5. Una reserva debe estar confirmada.
            Assert.Equal(
                1,
                resultados.Count(e =>
                    e == EstadoReserva.CONFIRMADA));

            // 6. La otra debe estar en espera.
            Assert.Equal(
                1,
                resultados.Count(e =>
                    e == EstadoReserva.EN_ESPERA));

            // 7. Verificar los datos realmente
            // guardados en SQL Server.
            await using var verificacion =
                new GenteFitDbContext(opciones);

            int totalConfirmadas =
                await verificacion.Reservas
                    .AsNoTracking()
                    .CountAsync(r =>
                        r.SesionId == sesionId &&
                        r.Estado ==
                            EstadoReserva.CONFIRMADA);

            int totalEnEspera =
                await verificacion.Reservas
                    .AsNoTracking()
                    .CountAsync(r =>
                        r.SesionId == sesionId &&
                        r.Estado ==
                            EstadoReserva.EN_ESPERA);

            // Nunca puede superarse el aforo.
            Assert.Equal(16, totalConfirmadas);
            Assert.Equal(1, totalEnEspera);

            // Los dos clientes deben tener
            // exactamente una reserva cada uno.
            int reservasCliente16 =
                await verificacion.Reservas
                    .AsNoTracking()
                    .CountAsync(r =>
                        r.ClienteId == cliente16Id &&
                        r.SesionId == sesionId);

            int reservasCliente17 =
                await verificacion.Reservas
                    .AsNoTracking()
                    .CountAsync(r =>
                        r.ClienteId == cliente17Id &&
                        r.SesionId == sesionId);

            Assert.Equal(1, reservasCliente16);
            Assert.Equal(1, reservasCliente17);

            // No deben quedar plazas libres.
            var sesiones =
                new SesionRepositorio(verificacion);

            int? plazasLibres =
                await sesiones.ObtenerPlazasLibresAsync(
                    sesionId);

            Assert.Equal(0, plazasLibres);
        }
        finally
        {
            // 8. Eliminar solamente la BD temporal.
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
