using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Persistencia.Repositorios;

// En el paso 3 añadiremos aquí las operaciones de reservar y anular.
public class ReservaRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public ReservaRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<int> ContarActivasClienteAsync(int clienteId, CancellationToken cancelacion = default)
    {
        return _contexto.Reservas
            .AsNoTracking()
            .CountAsync(r => r.ClienteId == clienteId &&
                (r.Estado == EstadoReserva.CONFIRMADA ||
                 r.Estado == EstadoReserva.EN_ESPERA), cancelacion);
    }
}
