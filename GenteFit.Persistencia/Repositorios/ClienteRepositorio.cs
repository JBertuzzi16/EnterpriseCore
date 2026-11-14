using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;

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
}