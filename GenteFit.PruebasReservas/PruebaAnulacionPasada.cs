
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

public static class PruebaAnulacionPasada
{
    public static async Task EjecutarAsync(
        GenteFitDbContext contexto,
        int sesionId)
    {
        Console.WriteLine();
        Console.WriteLine("PRUEBA DE ANULACION DE SESION PASADA");

        var repositorio = new ReservaRepositorio(contexto);

        // Utilizamos una sesión existente como referencia.
        var original = await contexto.Sesiones
            .AsNoTracking()
            .SingleAsync(s => s.Id == sesionId);

        // Creamos una sesión que tuvo lugar hace dos días.
        DateTime inicio = DateTime.Now.Date
            .AddDays(-2)
            .AddHours(18);

        var sesionPasada = new Sesion
        {
            ActividadId = original.ActividadId,
            SalaId = original.SalaId,
            MonitorId = original.MonitorId,
            FechaHoraInicio = inicio,
            FechaHoraFin = inicio.AddHours(1),
            AforoMaximo = 16,
            Anulada = false
        };

        contexto.Sesiones.Add(sesionPasada);
        await contexto.SaveChangesAsync();

        // Seleccionamos dos clientes activos.
        var clientes = await contexto.Clientes
            .AsNoTracking()
            .Where(c => c.Activo && c.Usuario.Activo)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(2)
            .ToListAsync();

        if (clientes.Count != 2)
        {
            throw new Exception(
                "Se necesitan dos clientes activos.");
        }

        // Insertamos directamente las reservas de prueba.
        // CrearAsync no permitiría reservar sesiones pasadas.
        var confirmada = new Reserva
        {
            ClienteId = clientes[0],
            SesionId = sesionPasada.Id,
            Estado = EstadoReserva.CONFIRMADA,
            FechaHoraReserva = DateTime.UtcNow.AddDays(-3)
        };

        var enEspera = new Reserva
        {
            ClienteId = clientes[1],
            SesionId = sesionPasada.Id,
            Estado = EstadoReserva.EN_ESPERA,
            FechaHoraReserva = DateTime.UtcNow.AddDays(-3)
                .AddMinutes(1)
        };

        contexto.Reservas.AddRange(confirmada, enEspera);
        await contexto.SaveChangesAsync();

        // Intentamos anular la reserva confirmada.
        bool anulacionRechazada = false;

        try
        {
            await repositorio.AnularAsync(
                clientes[0], confirmada.Id);
        }
        catch (GenteFitException ex)
        {
            if (ex.Message.Contains(
                "ya ha comenzado",
                StringComparison.OrdinalIgnoreCase))
            {
                anulacionRechazada = true;

                Console.WriteLine(
                    "OK - Anulación de sesión pasada rechazada.");
            }
            else
            {
                throw new Exception(
                    "Motivo de rechazo inesperado: " + ex.Message);
            }
        }

        if (!anulacionRechazada)
        {
            throw new Exception(
                "Se permitió anular una sesión pasada.");
        }

        // Comprobamos los valores reales de SQL Server.
        var estadoConfirmada = await contexto.Reservas
            .AsNoTracking()
            .SingleAsync(r => r.Id == confirmada.Id);

        var estadoEspera = await contexto.Reservas
            .AsNoTracking()
            .SingleAsync(r => r.Id == enEspera.Id);

        if (estadoConfirmada.Estado !=
                EstadoReserva.CONFIRMADA ||
            estadoConfirmada.FechaHoraAnulacion != null)
        {
            throw new Exception(
                "La reserva confirmada se ha modificado.");
        }

        if (estadoEspera.Estado != EstadoReserva.EN_ESPERA)
        {
            throw new Exception(
                "La lista de espera se ha modificado.");
        }

        Console.WriteLine(
            "OK - Reserva confirmada sin modificaciones.");

        Console.WriteLine(
            "OK - Lista de espera sin modificaciones.");

        Console.WriteLine();
        Console.WriteLine(
            "PRUEBA DE ANULACION PASADA SUPERADA.");
    }
}
