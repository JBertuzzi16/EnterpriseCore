using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;
using GenteFit.Persistencia.Seguridad;


namespace GenteFit.Persistencia.Repositorios;

public class ClientePerfilResumen
{
    public string Nombre { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string Dni { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefono { get; set; } = "";

    public DateTime FechaNacimiento { get; set; }
    public DateTime FechaAlta { get; set; }
    public bool Activo { get; set; }

    public string NombreUsuario { get; set; } = "";
    public string EmailCuenta { get; set; } = "";
    public bool CuentaActiva { get; set; }
}

public class ClienteRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public ClienteRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    public List<Cliente> ObtenerTodos()
    {
        return _contexto.Clientes
            .OrderBy(cliente => cliente.Apellidos)
            .ThenBy(cliente => cliente.Nombre)
            .ToList();
    }

    public Cliente? ObtenerPorId(int id)
    {
        return _contexto.Clientes.Find(id);
    }

    public Task<ClientePerfilResumen?> ObtenerPerfilAsync(
        int clienteId,
        CancellationToken cancelacion = default)
    {
        if (clienteId <= 0)
            return Task.FromResult<ClientePerfilResumen?>(null);

        return _contexto.Clientes
            .AsNoTracking()
            .Where(c => c.Id == clienteId)
            .Select(c => new ClientePerfilResumen
            {
                Nombre = c.Nombre,
                Apellidos = c.Apellidos,
                Dni = c.Dni,
                Email = c.Email,
                Telefono = c.Telefono,
                FechaNacimiento = c.FechaNacimiento,
                FechaAlta = c.FechaAlta,
                Activo = c.Activo,
                NombreUsuario = c.Usuario.NombreUsuario,
                EmailCuenta = c.Usuario.Email,
                CuentaActiva = c.Usuario.Activo
            })
            .SingleOrDefaultAsync(cancelacion);
    }

    public Cliente Crear (Cliente cliente, string contrasena)
    {
        cliente.Dni = cliente.Dni.Trim().ToUpperInvariant();
        cliente.Email = cliente.Email.Trim().ToUpperInvariant();

        if (_contexto.Clientes.Any(c => c.Dni.Equals(cliente.Dni)))
            throw new ArgumentException("Ya existe un cliente con ese número de DNI");
        if(_contexto.Clientes.Any(c=>c.Email.Equals(cliente.Email) ||_contexto.Usuarios.Any(u => u.Email.Equals(cliente.Email))))
        {
            throw new ArgumentException("Correo ya está registrado");
        }

        cliente.Usuario = new Usuario
        {
            NombreUsuario = $"cliente_{Guid.NewGuid():N}",
            Email = cliente.Email,
            HashContrasena = HashContrasena.Crear(contrasena),
            Rol = Rol.CLIENTE,
            Activo = true
        };

        cliente.FechaAlta = DateTime.Today;
        cliente.Activo = true;
        _contexto.Clientes.Add(cliente);
        _contexto.SaveChanges();

        return cliente;

    }

    public bool DarDeBaja(int id)
    {
        var cliente = _contexto.Clientes.Include(c=>c.Usuario).SingleOrDefault(c=>c.Id.Equals(id));

        if (cliente == null)
            return false;

        var ahora = DateTime.Now;

        bool tieneReservasPendientes = _contexto.Reservas.Any(
            r=>r.ClienteId.Equals(id) && 
            !r.Sesion.Anulada && 
            r.Sesion.FechaHoraInicio>ahora 
            && (r.Estado.Equals(EstadoReserva.CONFIRMADA)
            ||r.Estado.Equals(EstadoReserva.EN_ESPERA)));

        if (tieneReservasPendientes)
            throw new InvalidOperationException("El cliente tiene reservas futuras. Anúlalas antes de darlo de baja.");

        cliente.Activo = false;
        cliente.Usuario.Activo = false;
        _contexto.SaveChanges();
        return true;

    }

    public bool Reactivar (int id)
    {
        var cliente = _contexto.Clientes
       .Include(c => c.Usuario)
       .SingleOrDefault(c => c.Id == id);

        if (cliente == null)
            return false;

        cliente.Activo = true;
        cliente.Usuario.Activo = true;
        _contexto.SaveChanges();

        return true;
    }
}