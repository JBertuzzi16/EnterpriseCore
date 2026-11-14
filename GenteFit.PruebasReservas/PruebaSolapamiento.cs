
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

public static class PruebaSolapamiento
{
    public static async Task EjecutarAsync(
        GenteFitDbContext contexto,
        int sesionId)
    {
        Console.WriteLine();
        Console.WriteLine("PRUEBAS DE SOLAPAMIENTO");

        var repositorio = new ReservaRepositorio(contexto);

        var sesionOriginal = await contexto.Sesiones
            .AsNoTracking()
            .SingleAsync(s => s.Id == sesionId);

        // Obtenemos un cliente que ya tiene una
        // reserva confirmada en la sesión original.
        int clienteConfirmadoId = await contexto.Reservas
            .AsNoTracking()
            .Where(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA)
            .OrderBy(r => r.Id)
            .Select(r => r.ClienteId)
            .FirstAsync();

        // Preparamos otro monitor y otra sala para
        // crear una sesión solapada de forma realista.
        var usuarioMonitor = new Usuario
        {
            NombreUsuario = "monitor_solapamiento",
            Email = "monitor.solape@gentefit.test",
            HashContrasena = "SOLO_PARA_PRUEBAS",
            Rol = Rol.MONITOR,
            Activo = true
        };

        var monitor = new GenteFit.Persistencia.Entidades.Monitor
        {
            Dni = "70000200B",
            Nombre = "Monitor",
            Apellidos = "Solapamiento",
            Email = usuarioMonitor.Email,
            Telefono = "600000200",
            Especialidad = "Yoga",
            Activo = true,
            Usuario = usuarioMonitor
        };

        var sala = new Sala
        {
            Nombre = "Sala Solapamiento",
            Ubicacion = "Planta 2",
            Activa = true
        };

        contexto.AddRange(monitor, sala);
        await contexto.SaveChangesAsync();

        // Sesión que empieza 30 minutos después
        // de la original: sus horarios coinciden.
        var sesionSolapada = new Sesion
        {
            ActividadId = sesionOriginal.ActividadId,
            SalaId = sala.Id,
            MonitorId = monitor.Id,
            FechaHoraInicio =
                sesionOriginal.FechaHoraInicio.AddMinutes(30),
            FechaHoraFin =
                sesionOriginal.FechaHoraFin.AddMinutes(30),
            AforoMaximo = 16,
            Anulada = false
        };

        // Sesión que comienza justo al terminar la
        // original: sus horarios no coinciden.
        var sesionContigua = new Sesion
        {
            ActividadId = sesionOriginal.ActividadId,
            SalaId = sesionOriginal.SalaId,
            MonitorId = sesionOriginal.MonitorId,
            FechaHoraInicio = sesionOriginal.FechaHoraFin,
            FechaHoraFin =
                sesionOriginal.FechaHoraFin.AddHours(1),
            AforoMaximo = 16,
            Anulada = false
        };

        contexto.Sesiones.AddRange(
            sesionSolapada, sesionContigua);

        await contexto.SaveChangesAsync();

        // PRUEBA 1: reserva confirmada solapada.
        bool solapamientoRechazado = false;

        try
        {
            await repositorio.CrearAsync(
                clienteConfirmadoId, sesionSolapada.Id);
        }
        catch (GenteFitException ex)
            when (ex.Message.Contains(
                "horario",
                StringComparison.OrdinalIgnoreCase))
        {
            solapamientoRechazado = true;
        }

        if (!solapamientoRechazado)
        {
            throw new Exception(
                "Se permitió una reserva con horarios solapados.");
        }

        Console.WriteLine(
            "OK - Solapamiento con reserva confirmada rechazado.");

        // Creamos otro cliente para comprobar el
        // comportamiento de la lista de espera.
        var usuarioEspera = new Usuario
        {
            NombreUsuario = "cliente_solape_espera",
            Email = "solape.espera@gentefit.test",
            HashContrasena = "SOLO_PARA_PRUEBAS",
            Rol = Rol.CLIENTE,
            Activo = true
        };

        var clienteEspera = new Cliente
        {
            Dni = "70000201C",
            Nombre = "Cliente",
            Apellidos = "Solapamiento Espera",
            Email = usuarioEspera.Email,
            Telefono = "600000201",
            FechaNacimiento = new DateTime(1995, 1, 1),
            FechaAlta = DateTime.Today,
            Activo = true,
            Usuario = usuarioEspera
        };

        contexto.Clientes.Add(clienteEspera);
        await contexto.SaveChangesAsync();

        // La sesión original sigue completa.
        var reservaEspera = await repositorio.CrearAsync(
            clienteEspera.Id, sesionId);

        if (reservaEspera.Estado != EstadoReserva.EN_ESPERA)
        {
            throw new Exception(
                "El cliente debería entrar en lista de espera.");
        }

        // PRUEBA 2: una reserva en espera también
        // impide reservar otra sesión solapada.
        bool esperaRechazada = false;

        try
        {
            await repositorio.CrearAsync(
                clienteEspera.Id, sesionSolapada.Id);
        }
        catch (GenteFitException ex)
            when (ex.Message.Contains(
                "horario",
                StringComparison.OrdinalIgnoreCase))
        {
            esperaRechazada = true;
        }

        if (!esperaRechazada)
        {
            throw new Exception(
                "Se permitió solapar una reserva en espera.");
        }

        Console.WriteLine(
            "OK - Solapamiento con lista de espera rechazado.");

        // Comprobamos que las operaciones rechazadas
        // no hayan generado reservas.
        int reservasSolapadas = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionSolapada.Id);

        if (reservasSolapadas != 0)
        {
            throw new Exception(
                "Se han guardado reservas solapadas.");
        }

        // PRUEBA 3: dos sesiones consecutivas
        // deben poder reservarse.
        var reservaContigua = await repositorio.CrearAsync(
            clienteConfirmadoId, sesionContigua.Id);

        if (reservaContigua.Estado != EstadoReserva.CONFIRMADA)
        {
            throw new Exception(
                "La reserva consecutiva debería confirmarse.");
        }

        bool guardada = await contexto.Reservas
            .AsNoTracking()
            .AnyAsync(r =>
                r.Id == reservaContigua.Id &&
                r.Estado == EstadoReserva.CONFIRMADA);

        if (!guardada)
        {
            throw new Exception(
                "La reserva consecutiva no se ha guardado.");
        }

        Console.WriteLine(
            "OK - Reserva consecutiva permitida y guardada.");

        Console.WriteLine();
        Console.WriteLine(
            "TODAS LAS PRUEBAS DE SOLAPAMIENTO SUPERADAS.");
    }
}
