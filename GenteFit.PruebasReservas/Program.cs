
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

// Buscar la conexión del proyecto web.
string ruta = BuscarConfiguracion();

using var documento = JsonDocument.Parse(
    File.ReadAllText(ruta),
    new JsonDocumentOptions
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    });

string cadenaOriginal = documento.RootElement
    .GetProperty("ConnectionStrings")
    .GetProperty("GenteFit")
    .GetString()
    ?? throw new Exception("No se encontró la conexión GenteFit.");

// Usamos una BD nueva y temporal.
// Nunca utilizamos directamente la BD GenteFit.
var conexion = new SqlConnectionStringBuilder(cadenaOriginal);

conexion.InitialCatalog =
    "GenteFit_Pruebas_" + Guid.NewGuid().ToString("N");

var opciones = new DbContextOptionsBuilder<GenteFitDbContext>()
    .UseSqlServer(conexion.ConnectionString)
    .Options;

bool baseCreada = false;

try
{
    await using var contexto = new GenteFitDbContext(opciones);

    await contexto.Database.EnsureCreatedAsync();
    baseCreada = true;

    Console.WriteLine("Base temporal creada correctamente.");

    // Crear una cuenta de cliente.
    var usuarioCliente = new Usuario
    {
        NombreUsuario = "cliente_prueba",
        Email = "cliente.prueba@gentefit.test",
        HashContrasena = "SOLO_PARA_PRUEBAS",
        Rol = Rol.CLIENTE,
        Activo = true
    };

    var cliente = new Cliente
    {
        Dni = "00000000A",
        Nombre = "Cliente",
        Apellidos = "De Prueba",
        Email = "cliente.prueba@gentefit.test",
        Telefono = "600000000",
        FechaNacimiento = new DateTime(1995, 1, 1),
        FechaAlta = DateTime.Today,
        Activo = true,
        Usuario = usuarioCliente
    };

    // Crear un monitor.
    var usuarioMonitor = new Usuario
    {
        NombreUsuario = "monitor_prueba",
        Email = "monitor.prueba@gentefit.test",
        HashContrasena = "SOLO_PARA_PRUEBAS",
        Rol = Rol.MONITOR,
        Activo = true
    };

    var monitor = new GenteFit.Persistencia.Entidades.Monitor
    {
        Dni = "00000001B",
        Nombre = "Marta",
        Apellidos = "Pruebas",
        Email = "monitor.prueba@gentefit.test",
        Telefono = "600000001",
        Especialidad = "Yoga",
        Activo = true,
        Usuario = usuarioMonitor
    };

    var sala = new Sala
    {
        Nombre = "Sala de prueba",
        Ubicacion = "Planta 1",
        Activa = true
    };

    var actividad = new Actividad
    {
        Nombre = "Yoga de prueba",
        Descripcion = "Sesión para probar reservas",
        Intensidad = NivelIntensidad.BAJA,
        DuracionMinutos = 60,
        Activa = true
    };

    contexto.AddRange(cliente, monitor, sala, actividad);
    await contexto.SaveChangesAsync();

    // La sesión tendrá lugar dentro de dos días.
    DateTime inicio = DateTime.Now.Date
        .AddDays(2)
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

    var repositorio = new ReservaRepositorio(contexto);

    // PRUEBA 1: reservar con plazas disponibles.
    var reserva = await repositorio.CrearAsync(
        cliente.Id, sesion.Id);

    if (reserva.Estado != EstadoReserva.CONFIRMADA)
        throw new Exception(
            "La reserva debería estar CONFIRMADA.");

    int total = await contexto.Reservas.CountAsync();

    if (total != 1)
        throw new Exception(
            "Debería existir exactamente una reserva.");

    Console.WriteLine("OK - Reserva confirmada y guardada.");

    // PRUEBA 2: intentar duplicar la reserva.
    bool duplicadaRechazada = false;

    try
    {
        await repositorio.CrearAsync(cliente.Id, sesion.Id);
    }
    catch (GenteFitException ex)
    {
        duplicadaRechazada = true;
        Console.WriteLine(
            "OK - Reserva duplicada rechazada: " + ex.Message);
    }

    if (!duplicadaRechazada)
        throw new Exception(
            "ERROR: El sistema permitió una reserva duplicada.");

    total = await contexto.Reservas.CountAsync();

    if (total != 1)
        throw new Exception(
            "ERROR: Se han guardado reservas adicionales.");

    Console.WriteLine();
    Console.WriteLine("LAS DOS PRUEBAS HAN SIDO SUPERADAS.");
    await PruebaListaEspera.EjecutarAsync(
       contexto, sesion.Id);
    await PruebaAnulacion.EjecutarAsync(
       contexto, sesion.Id);
    await PruebaValidaciones.EjecutarAsync(
       contexto, sesion.Id);
    await PruebaSolapamiento.EjecutarAsync(
       contexto, sesion.Id);
    await PruebaConcurrencia.EjecutarAsync(
       contexto, sesion.Id);
    await PruebaAnulacionPasada.EjecutarAsync(
       contexto, sesion.Id);
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("PRUEBA FALLIDA: " + ex.Message);
}
finally
{
    // Eliminar únicamente la BD temporal creada.
    if (baseCreada)
    {
        await using var limpieza = new GenteFitDbContext(opciones);
        await limpieza.Database.EnsureDeletedAsync();

        Console.WriteLine("Base temporal eliminada.");
    }
}

Console.WriteLine("Pulsa Enter para finalizar.");
Console.ReadLine();

// Buscar appsettings.json desde la solución.
static string BuscarConfiguracion()
{
    DirectoryInfo? carpeta =
        new DirectoryInfo(AppContext.BaseDirectory);

    while (carpeta != null)
    {
        string candidato = Path.Combine(
            carpeta.FullName,
            "GenteFit.web",
            "appsettings.json");

        if (File.Exists(candidato))
            return candidato;

        carpeta = carpeta.Parent;
    }

    throw new FileNotFoundException(
        "No se encontró GenteFit.web/appsettings.json.");
}
