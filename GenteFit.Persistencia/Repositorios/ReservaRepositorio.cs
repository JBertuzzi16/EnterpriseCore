using System.Data;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Persistencia.Repositorios;

public class ReservaRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public ReservaRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    // Contador utilizado en el menú del cliente.
    public Task<int> ContarActivasClienteAsync(
        int clienteId,
        CancellationToken cancelacion = default)
    {
        return _contexto.Reservas
            .AsNoTracking()
            .CountAsync(r =>
                r.ClienteId == clienteId &&
                (r.Estado == EstadoReserva.CONFIRMADA ||
                 r.Estado == EstadoReserva.EN_ESPERA),
                cancelacion);
    }

    // Crea una reserva confirmada o en lista de espera.
    public async Task<Reserva> CrearAsync(
        int clienteId,
        int sesionId,
        CancellationToken cancelacion = default)
    {
        if (clienteId <= 0 || sesionId <= 0)
        {
            throw new GenteFitException(
                "Los datos de la reserva no son válidos.");
        }

        // Todas las comprobaciones y el INSERT
        // forman parte de la misma transacción.
        await using var transaccion =
            await _contexto.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancelacion);

        try
        {
            // Bloqueamos el cliente para impedir reservas
            // simultáneas que puedan solaparse.
            var clientes = await _contexto.Clientes
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Clientes] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {clienteId}")
                .AsNoTracking()
                .ToListAsync(cancelacion);

            Cliente? cliente = clientes.SingleOrDefault();

            if (cliente == null || !cliente.Activo)
            {
                throw new GenteFitException(
                    "El cliente no existe o está dado de baja.");
            }

            bool usuarioActivo = await _contexto.Usuarios
                .AnyAsync(u =>
                    u.Id == cliente.UsuarioId && u.Activo,
                    cancelacion);

            if (!usuarioActivo)
            {
                throw new GenteFitException(
                    "La cuenta del cliente no está activa.");
            }

            // Bloqueamos la sesión para proteger el aforo.
            var sesiones = await _contexto.Sesiones
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Sesiones] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {sesionId}")
                .AsNoTracking()
                .ToListAsync(cancelacion);

            Sesion? sesion = sesiones.SingleOrDefault();

            if (sesion == null)
            {
                throw new GenteFitException(
                    "La sesión seleccionada no existe.");
            }

            if (sesion.Anulada)
            {
                throw new GenteFitException(
                    "No puedes reservar una sesión anulada.");
            }

            if (sesion.FechaHoraInicio <= DateTime.Now)
            {
                throw new GenteFitException(
                    "No puedes reservar una sesión que ya ha comenzado.");
            }

            // Comprobamos que los recursos de la sesión
            // siguen activos.
            var disponibilidad = await _contexto.Sesiones
                .Where(s => s.Id == sesionId)
                .Select(s => new
                {
                    ActividadActiva = s.Actividad.Activa,
                    SalaActiva = s.Sala.Activa,
                    MonitorActivo = s.Monitor.Activo
                })
                .SingleAsync(cancelacion);

            if (!disponibilidad.ActividadActiva ||
                !disponibilidad.SalaActiva ||
                !disponibilidad.MonitorActivo)
            {
                throw new GenteFitException(
                    "Esta sesión ya no está disponible.");
            }

            // No permitimos dos reservas activas
            // del mismo cliente para la misma sesión.
            bool duplicada = await _contexto.Reservas
                .AnyAsync(r =>
                    r.ClienteId == clienteId &&
                    r.SesionId == sesionId &&
                    r.Estado != EstadoReserva.ANULADA,
                    cancelacion);

            if (duplicada)
            {
                throw new GenteFitException(
                    "Ya tienes una reserva activa para esta sesión.");
            }

            // Comprobamos los solapamientos de horario.
            // También cuentan las reservas en espera.
            bool solapamiento = await _contexto.Reservas
                .AnyAsync(r =>
                    r.ClienteId == clienteId &&
                    r.Estado != EstadoReserva.ANULADA &&
                    r.Sesion.FechaHoraInicio < sesion.FechaHoraFin &&
                    r.Sesion.FechaHoraFin > sesion.FechaHoraInicio,
                    cancelacion);

            if (solapamiento)
            {
                throw new GenteFitException(
                    "Ya tienes otra reserva en ese horario.");
            }

            // Contamos únicamente plazas confirmadas.
            int ocupadas = await _contexto.Reservas
                .CountAsync(r =>
                    r.SesionId == sesionId &&
                    r.Estado == EstadoReserva.CONFIRMADA,
                    cancelacion);

            EstadoReserva estado = ocupadas < sesion.AforoMaximo
                ? EstadoReserva.CONFIRMADA
                : EstadoReserva.EN_ESPERA;

            var reserva = new Reserva
            {
                ClienteId = clienteId,
                SesionId = sesionId,
                FechaHoraReserva = DateTime.UtcNow,
                Estado = estado,
                FechaHoraAnulacion = null
            };

            _contexto.Reservas.Add(reserva);

            await _contexto.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);

            return reserva;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627))
        {
            throw new GenteFitException(
                "Ya existe una reserva activa para esta sesión.",
                ex);
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new GenteFitException(
                "Se están procesando varias reservas a la vez. Inténtalo de nuevo.",
                ex);
        }
    }

    // Anula una reserva propia y, si libera una plaza,
    // promociona al primer cliente en espera.
    public async Task<ResultadoAnulacion> AnularAsync(
        int clienteId,
        int reservaId,
        CancellationToken cancelacion = default)
    {
        if (clienteId <= 0 || reservaId <= 0)
        {
            throw new GenteFitException(
                "Los datos de la reserva no son válidos.");
        }

        await using var transaccion =
            await _contexto.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancelacion);

        try
        {
            // Solo buscamos reservas del cliente indicado.
            int? sesionId = await _contexto.Reservas
                .AsNoTracking()
                .Where(r =>
                    r.Id == reservaId &&
                    r.ClienteId == clienteId)
                .Select(r => (int?)r.SesionId)
                .SingleOrDefaultAsync(cancelacion);

            if (!sesionId.HasValue)
            {
                throw new GenteFitException(
                    "No se ha encontrado una reserva tuya con ese identificador.");
            }

            // Mantenemos el orden de bloqueos usado en
            // CrearAsync: primero cliente y después sesión.
            var clientes = await _contexto.Clientes
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Clientes] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {clienteId}")
                .AsNoTracking()
                .ToListAsync(cancelacion);

            if (clientes.Count != 1)
            {
                throw new GenteFitException(
                    "El cliente no existe.");
            }

            // Bloqueamos la sesión durante la operación.
            var sesiones = await _contexto.Sesiones
                .FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[Sesiones] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {sesionId.Value}")
                .AsNoTracking()
                .ToListAsync(cancelacion);

            Sesion? sesion = sesiones.SingleOrDefault();

            if (sesion == null)
            {
                throw new GenteFitException(
                    "La sesión no existe.");
            }

            if (sesion.FechaHoraInicio <= DateTime.Now)
            {
                throw new GenteFitException(
                    "No puedes anular una reserva de una sesión que ya ha comenzado.");
            }

            // Volvemos a consultar la reserva una vez
            // bloqueada la sesión.
            Reserva? reserva = await _contexto.Reservas
                .SingleOrDefaultAsync(r =>
                    r.Id == reservaId &&
                    r.ClienteId == clienteId &&
                    r.SesionId == sesionId.Value,
                    cancelacion);

            if (reserva == null ||
                reserva.Estado == EstadoReserva.ANULADA)
            {
                throw new GenteFitException(
                    "La reserva no existe o ya está anulada.");
            }

            bool estabaConfirmada =
                reserva.Estado == EstadoReserva.CONFIRMADA;

            // Anulamos la reserva.
            reserva.Estado = EstadoReserva.ANULADA;
            reserva.FechaHoraAnulacion = DateTime.UtcNow;

            int? clientePromocionadoId = null;
            int? reservaPromocionadaId = null;

            // Solo promocionamos cuando se libera una
            // plaza que anteriormente estaba confirmada.
            if (estabaConfirmada)
            {
                Reserva? siguiente = await _contexto.Reservas
                    .Where(r =>
                        r.SesionId == sesionId.Value &&
                        r.Estado == EstadoReserva.EN_ESPERA)
                    .OrderBy(r => r.FechaHoraReserva)
                    .ThenBy(r => r.Id)
                    .FirstOrDefaultAsync(cancelacion);

                if (siguiente != null)
                {
                    siguiente.Estado =
                        EstadoReserva.CONFIRMADA;

                    clientePromocionadoId =
                        siguiente.ClienteId;

                    reservaPromocionadaId =
                        siguiente.Id;
                }
            }

            // Guardamos la anulación y la posible
            // promoción en una única transacción.
            await _contexto.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);

            return new ResultadoAnulacion
            {
                ReservaAnuladaId = reservaId,
                ClientePromocionadoId = clientePromocionadoId,
                ReservaPromocionadaId = reservaPromocionadaId
            };
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new GenteFitException(
                "Hay varias operaciones en curso. Inténtalo de nuevo.",
                ex);
        }
    }
}
public class ResultadoAnulacion
{
    public int ReservaAnuladaId { get; set; }

    public int? ClientePromocionadoId { get; set; }

    public int? ReservaPromocionadaId { get; set; }

    public bool HayPromocion =>
        ClientePromocionadoId.HasValue;
}
