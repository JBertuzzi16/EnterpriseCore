
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

public static class PruebaValidaciones
{
    public static async Task EjecutarAsync(
        GenteFitDbContext contexto,
        int sesionId)
    {
        Console.WriteLine();
        Console.WriteLine("PRUEBAS DE VALIDACIONES");

        var repositorio = new ReservaRepositorio(contexto);

        // Obtenemos una sesión existente como referencia.
        var sesionOriginal = await contexto.Sesiones
            .AsNoTracking()
            .SingleAsync(s => s.Id == sesionId);

        // PRUEBA 1: sesión que ya ha comenzado.
        DateTime inicioPasado = DateTime.Now.AddDays(-2);

        var sesionPasada = new Sesion
        {
            ActividadId = sesionOriginal.ActividadId,
            SalaId = sesionOriginal.SalaId,
            MonitorId = sesionOriginal.MonitorId,
            FechaHoraInicio = inicioPasado,
            FechaHoraFin = inicioPasado.AddHours(1),
            AforoMaximo = 16,
            Anulada = false
        };

        contexto.Sesiones.Add(sesionPasada);
        await contexto.SaveChangesAsync();

        int clienteId = await contexto.Clientes
            .Select(c => c.Id)
            .FirstAsync();

        bool sesionPasadaRechazada = false;

        try
        {
            await repositorio.CrearAsync(
                clienteId, sesionPasada.Id);
        }
        catch (GenteFitException ex)
        {
            if (ex.Message.Contains(
                "ya ha comenzado",
                StringComparison.OrdinalIgnoreCase))
            {
                sesionPasadaRechazada = true;
                Console.WriteLine(
                    "OK - Sesión pasada rechazada.");
            }
            else
            {
                throw new Exception(
                    "Se rechazó por un motivo inesperado: "
                    + ex.Message);
            }
        }

        if (!sesionPasadaRechazada)
        {
            throw new Exception(
                "Se permitió reservar una sesión pasada.");
        }

        // PRUEBA 2: cliente dado de baja.
        var usuarioInactivo = new Usuario
        {
            NombreUsuario = "cliente_inactivo_prueba",
            Email = "inactivo@gentefit.test",
            HashContrasena = "SOLO_PARA_PRUEBAS",
            Rol = Rol.CLIENTE,
            Activo = true
        };

        var clienteInactivo = new Cliente
        {
            Dni = "70000100A",
            Nombre = "Cliente",
            Apellidos = "Inactivo",
            Email = usuarioInactivo.Email,
            Telefono = "600000100",
            FechaNacimiento = new DateTime(1995, 1, 1),
            FechaAlta = DateTime.Today,
            Activo = false,
            Usuario = usuarioInactivo
        };

        contexto.Clientes.Add(clienteInactivo);
        await contexto.SaveChangesAsync();

        bool clienteRechazado = false;

        try
        {
            await repositorio.CrearAsync(
                clienteInactivo.Id, sesionId);
        }
        catch (GenteFitException ex)
        {
            if (ex.Message.Contains(
                "dado de baja",
                StringComparison.OrdinalIgnoreCase))
            {
                clienteRechazado = true;
                Console.WriteLine(
                    "OK - Cliente inactivo rechazado.");
            }
            else
            {
                throw new Exception(
                    "Se rechazó por un motivo inesperado: "
                    + ex.Message);
            }
        }

        if (!clienteRechazado)
        {
            throw new Exception(
                "Se permitió reservar a un cliente inactivo.");
        }

        // Comprobar que las operaciones rechazadas
        // no han generado nuevas reservas.
        bool reservaIndebida = await contexto.Reservas
            .AnyAsync(r =>
                r.SesionId == sesionPasada.Id ||
                r.ClienteId == clienteInactivo.Id);

        if (reservaIndebida)
        {
            throw new Exception(
                "Se ha guardado una reserva inválida.");
        }

        Console.WriteLine(
            "OK - No se han creado reservas inválidas.");

        Console.WriteLine();
        Console.WriteLine(
            "TODAS LAS VALIDACIONES SUPERADAS.");
    }
}
