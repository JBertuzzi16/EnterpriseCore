using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Persistencia.Repositorios;

public class UsuarioRepositorio
{
    private readonly GenteFitDbContext _contexto;

    public UsuarioRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    public Usuario? ObtenerPorEmail(string email)
    {
        string limpio = email.Trim();

        return _contexto.Usuarios
            .Include(u => u.Cliente)
            .FirstOrDefault(u => u.Email == limpio);
    }

    // Devuelve el usuario si el email existe, la cuenta está activa y la contraseña coincide.
    // Devuelve null en cualquier otro caso (así no se revela qué ha fallado).
    public Usuario? ValidarCredenciales(string email, string contrasena)
    {
        Usuario? usuario = ObtenerPorEmail(email);

        if (usuario == null || !usuario.Activo)
            return null;

        return HashContrasena.Verificar(contrasena, usuario.HashContrasena)
            ? usuario
            : null;
    }

    public Usuario Crear(string nombreUsuario, string email, string contrasena, Rol rol)
    {
        var usuario = new Usuario
        {
            NombreUsuario = nombreUsuario.Trim(),
            Email = email.Trim(),
            HashContrasena = HashContrasena.Crear(contrasena),
            Rol = rol
        };

        _contexto.Usuarios.Add(usuario);
        _contexto.SaveChanges();

        return usuario;
    }
}
