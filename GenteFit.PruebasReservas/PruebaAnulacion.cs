
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

public static class PruebaAnulacion
{
    public static async Task EjecutarAsync(
        GenteFitDbContext contexto,
        int sesionId)
    {
        Console.WriteLine();
        Console.WriteLine("PRUEBAS DE ANULACION");

        var repositorio = new ReservaRepositorio(contexto);

        // Comprobamos el estado dejado por la prueba anterior.
        int confirmadasIniciales = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA);

        var primeraEspera = await contexto.Reservas
            .AsNoTracking()
            .Where(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.EN_ESPERA)
            .OrderBy(r => r.FechaHoraReserva)
            .ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.ClienteId })
            .FirstOrDefaultAsync();

        if (confirmadasIniciales != 16 ||
            primeraEspera == null)
        {
            throw new Exception(
                "La sesión debe tener 16 reservas confirmadas " +
                "y al menos una reserva en espera.");
        }

        // Creamos otro cliente para tener dos personas
        // en la lista de espera.
        var usuario = new Usuario
        {
            NombreUsuario = "cliente_espera_extra",
            Email = "espera.extra@gentefit.test",
            HashContrasena = "SOLO_PARA_PRUEBAS",
            Rol = Rol.CLIENTE,
            Activo = true
        };

        var cliente = new Cliente
        {
            Dni = "70000099A",
            Nombre = "Cliente",
            Apellidos = "Espera Extra",
            Email = usuario.Email,
            Telefono = "600000099",
            FechaNacimiento = new DateTime(1995, 1, 1),
            FechaAlta = DateTime.Today,
            Activo = true,
            Usuario = usuario
        };

        contexto.Clientes.Add(cliente);
        await contexto.SaveChangesAsync();

        var segundaEspera = await repositorio.CrearAsync(
            cliente.Id, sesionId);

        if (segundaEspera.Estado != EstadoReserva.EN_ESPERA)
        {
            throw new Exception(
                "La nueva reserva debería estar en espera.");
        }

        Console.WriteLine("OK - Dos clientes en lista de espera.");

        // PRUEBA 1: impedir anular una reserva ajena.
        bool reservaAjenaRechazada = false;

        try
        {
            await repositorio.AnularAsync(
                cliente.Id, primeraEspera.Id);
        }
        catch (GenteFitException)
        {
            reservaAjenaRechazada = true;
        }

        if (!reservaAjenaRechazada)
        {
            throw new Exception(
                "Se ha permitido anular una reserva ajena.");
        }

        Console.WriteLine(
            "OK - Anulación de reserva ajena rechazada.");

        // PRUEBA 2: cancelar la segunda reserva en espera.
        var resultadoEspera = await repositorio.AnularAsync(
            cliente.Id, segundaEspera.Id);

        if (resultadoEspera.HayPromocion)
        {
            throw new Exception(
                "Cancelar una reserva en espera no debe promocionar.");
        }

        var estadoSegunda = await contexto.Reservas
            .AsNoTracking()
            .Where(r => r.Id == segundaEspera.Id)
            .Select(r => r.Estado)
            .SingleAsync();

        int confirmadas = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA);

        int enEspera = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.EN_ESPERA);

        if (estadoSegunda != EstadoReserva.ANULADA ||
            confirmadas != 16 ||
            enEspera != 1)
        {
            throw new Exception(
                "La anulación en espera no ha funcionado correctamente.");
        }

        Console.WriteLine(
            "OK - Reserva en espera anulada sin promoción.");

        // PRUEBA 3: cancelar una reserva confirmada.
        var confirmada = await contexto.Reservas
            .AsNoTracking()
            .Where(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA)
            .OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.ClienteId })
            .FirstAsync();

        var resultado = await repositorio.AnularAsync(
            confirmada.ClienteId, confirmada.Id);

        if (!resultado.HayPromocion ||
            resultado.ReservaPromocionadaId != primeraEspera.Id ||
            resultado.ClientePromocionadoId != primeraEspera.ClienteId)
        {
            throw new Exception(
                "No se ha promocionado al primer cliente en espera.");
        }

        // Comprobar que los cambios están guardados.
        var reservaAnulada = await contexto.Reservas
            .AsNoTracking()
            .SingleAsync(r => r.Id == confirmada.Id);

        var reservaPromocionada = await contexto.Reservas
            .AsNoTracking()
            .SingleAsync(r => r.Id == primeraEspera.Id);

        confirmadas = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA);

        enEspera = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.EN_ESPERA);

        if (reservaAnulada.Estado != EstadoReserva.ANULADA ||
            reservaAnulada.FechaHoraAnulacion == null ||
            reservaPromocionada.Estado != EstadoReserva.CONFIRMADA ||
            confirmadas != 16 ||
            enEspera != 0)
        {
            throw new Exception(
                "La promoción no ha mantenido correctamente el aforo.");
        }

        Console.WriteLine(
            "OK - Primera reserva en espera promocionada.");
        Console.WriteLine(
            "OK - El aforo permanece en 16 plazas confirmadas.");

        Console.WriteLine();
        Console.WriteLine("TODAS LAS PRUEBAS DE ANULACION SUPERADAS.");
    }
}
