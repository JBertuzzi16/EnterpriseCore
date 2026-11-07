
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

public static class PruebaConcurrencia
{
    public static async Task EjecutarAsync(
        GenteFitDbContext contexto,
        int sesionId)
    {
        Console.WriteLine();
        Console.WriteLine("PRUEBA DE CONCURRENCIA");

        // Obtenemos una sesión como referencia.
        var original = await contexto.Sesiones
            .AsNoTracking()
            .SingleAsync(s => s.Id == sesionId);

        // Creamos otra sesión para esta prueba.
        // Así no modificamos las reservas anteriores.
        DateTime inicio = original.FechaHoraInicio.AddDays(14);

        var sesion = new Sesion
        {
            ActividadId = original.ActividadId,
            SalaId = original.SalaId,
            MonitorId = original.MonitorId,
            FechaHoraInicio = inicio,
            FechaHoraFin = inicio.AddHours(1),
            AforoMaximo = 16,
            Anulada = false
        };

        contexto.Sesiones.Add(sesion);
        await contexto.SaveChangesAsync();

        // Reutilizamos 17 clientes activos creados
        // por las pruebas anteriores.
        var clientes = await contexto.Clientes
            .AsNoTracking()
            .Where(c => c.Activo && c.Usuario.Activo)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(17)
            .ToListAsync();

        if (clientes.Count < 17)
        {
            throw new Exception(
                "Se necesitan 17 clientes activos.");
        }

        // Preparamos la sesión con 15 plazas ocupadas.
        for (int i = 0; i < 15; i++)
        {
            contexto.Reservas.Add(new Reserva
            {
                ClienteId = clientes[i],
                SesionId = sesion.Id,
                Estado = EstadoReserva.CONFIRMADA,
                FechaHoraReserva = DateTime.UtcNow
            });
        }

        await contexto.SaveChangesAsync();

        Console.WriteLine(
            "OK - Sesión preparada con 15 reservas.");

        // Cada cliente tendrá su propio DbContext.
        // No se puede compartir un mismo DbContext
        // entre operaciones paralelas.
        string cadenaConexion = contexto.Database
            .GetDbConnection().ConnectionString;

        var opciones =
            new DbContextOptionsBuilder<GenteFitDbContext>()
                .UseSqlServer(cadenaConexion)
                .Options;

        // Señal para iniciar las dos solicitudes
        // prácticamente al mismo tiempo.
        var inicioSimultaneo = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var tareaA = ReservarAsync(
            opciones,
            clientes[15],
            sesion.Id,
            inicioSimultaneo.Task);

        var tareaB = ReservarAsync(
            opciones,
            clientes[16],
            sesion.Id,
            inicioSimultaneo.Task);

        // Liberamos las dos solicitudes.
        inicioSimultaneo.SetResult(true);

        EstadoReserva[] resultados =
            await Task.WhenAll(tareaA, tareaB);

        int nuevasConfirmadas = resultados.Count(
            e => e == EstadoReserva.CONFIRMADA);

        int nuevasEnEspera = resultados.Count(
            e => e == EstadoReserva.EN_ESPERA);

        if (nuevasConfirmadas != 1 || nuevasEnEspera != 1)
        {
            throw new Exception(
                "Las reservas simultáneas no han sido asignadas correctamente.");
        }

        Console.WriteLine(
            "OK - Una reserva confirmada y otra en espera.");

        // Volvemos a consultar SQL Server para
        // comprobar el resultado almacenado.
        int confirmadas = await contexto.Reservas
            .AsNoTracking()
            .CountAsync(r =>
                r.SesionId == sesion.Id &&
                r.Estado == EstadoReserva.CONFIRMADA);

        int espera = await contexto.Reservas
            .AsNoTracking()
            .CountAsync(r =>
                r.SesionId == sesion.Id &&
                r.Estado == EstadoReserva.EN_ESPERA);

        if (confirmadas != 16 || espera != 1)
        {
            throw new Exception(
                "El aforo no se ha respetado.");
        }

        Console.WriteLine(
            "OK - SQL Server confirma 16 plazas y 1 en espera.");

        Console.WriteLine();
        Console.WriteLine(
            "PRUEBA DE CONCURRENCIA SUPERADA.");
    }

    private static async Task<EstadoReserva> ReservarAsync(
        DbContextOptions<GenteFitDbContext> opciones,
        int clienteId,
        int sesionId,
        Task inicio)
    {
        await inicio;

        // Contexto independiente para cada cliente.
        await using var contexto =
            new GenteFitDbContext(opciones);

        var repositorio = new ReservaRepositorio(contexto);

        var reserva = await repositorio.CrearAsync(
            clienteId,
            sesionId);

        return reserva.Estado;
    }
}
