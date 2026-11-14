
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

public static class PruebaListaEspera
{
    public static async Task EjecutarAsync(
        GenteFitDbContext contexto,
        int sesionId)
    {
        Console.WriteLine();
        Console.WriteLine("PRUEBA 3 - LISTA DE ESPERA");

        // Partimos de la reserva confirmada
        // que hemos creado en la prueba anterior.
        int confirmadasIniciales = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA);

        if (confirmadasIniciales != 1)
        {
            throw new Exception(
                "Se esperaba una reserva confirmada inicial.");
        }

        // Creamos 16 clientes adicionales de prueba.
        var clientes = new List<Cliente>();

        for (int i = 1; i <= 16; i++)
        {
            var usuario = new Usuario
            {
                NombreUsuario = $"cliente_lista_{i}",
                Email = $"lista{i}@gentefit.test",
                HashContrasena = "SOLO_PARA_PRUEBAS",
                Rol = Rol.CLIENTE,
                Activo = true
            };

            var cliente = new Cliente
            {
                Dni = $"{70000000 + i}A",
                Nombre = $"Cliente {i}",
                Apellidos = "Prueba Espera",
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
        await contexto.SaveChangesAsync();

        var repositorio = new ReservaRepositorio(contexto);

        // La sesión tiene una plaza ocupada.
        // Confirmamos otras 15 hasta alcanzar 16.
        for (int i = 0; i < 15; i++)
        {
            var reserva = await repositorio.CrearAsync(
                clientes[i].Id, sesionId);

            if (reserva.Estado != EstadoReserva.CONFIRMADA)
            {
                throw new Exception(
                    $"La reserva {i + 2} debería confirmarse.");
            }
        }

        Console.WriteLine("OK - Sesión completa con 16 plazas.");

        // El cliente número 17 intenta reservar.
        var reservaEspera = await repositorio.CrearAsync(
            clientes[15].Id, sesionId);

        if (reservaEspera.Estado != EstadoReserva.EN_ESPERA)
        {
            throw new Exception(
                "El cliente 17 debería entrar en espera.");
        }

        Console.WriteLine(
            "OK - Cliente 17 añadido a la lista de espera.");

        // Comprobar los estados almacenados en SQL Server.
        int confirmadas = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.CONFIRMADA);

        int enEspera = await contexto.Reservas
            .CountAsync(r =>
                r.SesionId == sesionId &&
                r.Estado == EstadoReserva.EN_ESPERA);

        if (confirmadas != 16 || enEspera != 1)
        {
            throw new Exception(
                "El número de reservas no es correcto.");
        }

        var sesiones = new SesionRepositorio(contexto);

        int? plazasLibres =
            await sesiones.ObtenerPlazasLibresAsync(sesionId);

        if (plazasLibres != 0)
        {
            throw new Exception(
                "Una sesión completa debe tener 0 plazas libres.");
        }

        Console.WriteLine(
            "OK - 16 confirmadas, 1 en espera y 0 plazas libres.");

        Console.WriteLine("PRUEBA DE LISTA DE ESPERA SUPERADA.");
    }
}
