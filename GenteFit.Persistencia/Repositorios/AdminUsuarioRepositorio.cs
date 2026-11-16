using System.Net.Mail;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Persistencia.Repositorios;

// Gestión de cuentas de usuario desde el panel del administrador.
// Reglas de negocio:
//  - El nombre de usuario y el email no se pueden repetir.
//  - Desde aquí solo se crean cuentas de personal (ADMIN, RECEPCIONISTA, ENCARGADO).
//    Las cuentas de cliente y de monitor nacen con su ficha (recepción / encargado).
//  - Un administrador no puede quitarse a sí mismo el rol ni desactivar su propia cuenta.
//  - Siempre tiene que quedar al menos un administrador activo.
public class AdminUsuarioRepositorio
{
    public const int LongitudMinimaContrasena = 8;

    // Roles que el administrador puede asignar a una cuenta sin ficha asociada.
    public static readonly Rol[] RolesDePersonal = { Rol.ADMIN, Rol.RECEPCIONISTA, Rol.ENCARGADO };

    private readonly GenteFitDbContext _contexto;

    public AdminUsuarioRepositorio(GenteFitDbContext contexto)
    {
        _contexto = contexto;
    }

    // ---------- Consultas ----------

    public List<Usuario> Listar(string? texto = null, Rol? rol = null, bool? activo = null)
    {
        IQueryable<Usuario> consulta = _contexto.Usuarios
            .AsNoTracking()
            .Include(u => u.Cliente)
            .Include(u => u.Monitor);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            string buscado = texto.Trim();
            consulta = consulta.Where(u =>
                u.NombreUsuario.Contains(buscado) || u.Email.Contains(buscado));
        }

        if (rol.HasValue)
            consulta = consulta.Where(u => u.Rol == rol.Value);

        if (activo.HasValue)
            consulta = consulta.Where(u => u.Activo == activo.Value);

        return consulta
            .OrderBy(u => u.Rol)
            .ThenBy(u => u.NombreUsuario)
            .ToList();
    }

    public Usuario? ObtenerPorId(int id) =>
        _contexto.Usuarios
            .Include(u => u.Cliente)
            .Include(u => u.Monitor)
            .FirstOrDefault(u => u.Id == id);

    public Dictionary<Rol, int> ContarPorRol() =>
        _contexto.Usuarios
            .AsNoTracking()
            .Where(u => u.Activo)
            .GroupBy(u => u.Rol)
            .Select(g => new { Rol = g.Key, Total = g.Count() })
            .ToDictionary(x => x.Rol, x => x.Total);

    // ---------- Altas y cambios ----------

    public Usuario Crear(string nombreUsuario, string email, string contrasena, Rol rol)
    {
        string nombre = ValidarNombreUsuario(nombreUsuario);
        string correo = ValidarEmail(email);
        ValidarContrasena(contrasena);

        if (!RolesDePersonal.Contains(rol))
            throw new GenteFitException(
                "Desde la administración solo se crean cuentas de personal. " +
                "Los clientes se dan de alta en recepción y los monitores desde su ficha.");

        ComprobarQueNoExiste(nombre, correo, idExcluido: null);

        var usuario = new Usuario
        {
            NombreUsuario = nombre,
            Email = correo,
            HashContrasena = HashContrasena.Crear(contrasena),
            Rol = rol,
            Activo = true
        };

        _contexto.Usuarios.Add(usuario);
        Guardar();

        return usuario;
    }

    public void Editar(int id, string nombreUsuario, string email, Rol rol, int idAdministradorActual)
    {
        Usuario usuario = ObtenerObligatorio(id);

        string nombre = ValidarNombreUsuario(nombreUsuario);
        string correo = ValidarEmail(email);
        ComprobarQueNoExiste(nombre, correo, idExcluido: id);

        if (rol != usuario.Rol)
            ComprobarCambioDeRol(usuario, rol, idAdministradorActual);

        usuario.NombreUsuario = nombre;
        usuario.Email = correo;
        usuario.Rol = rol;

        // El email de la ficha de cliente o monitor se mantiene igual que el de la cuenta.
        if (usuario.Cliente != null) usuario.Cliente.Email = correo;
        if (usuario.Monitor != null) usuario.Monitor.Email = correo;

        Guardar();
    }

    public void Desactivar(int id, int idAdministradorActual)
    {
        Usuario usuario = ObtenerObligatorio(id);

        if (usuario.Id == idAdministradorActual)
            throw new GenteFitException("No puedes desactivar tu propia cuenta.");

        if (usuario.Rol == Rol.ADMIN && usuario.Activo && EsUltimoAdministradorActivo(usuario.Id))
            throw new GenteFitException("Debe quedar al menos un administrador activo.");

        usuario.Activo = false;
        Guardar();
    }

    public void Activar(int id)
    {
        Usuario usuario = ObtenerObligatorio(id);
        usuario.Activo = true;
        Guardar();
    }

    public void RestablecerContrasena(int id, string nuevaContrasena)
    {
        ValidarContrasena(nuevaContrasena);

        Usuario usuario = ObtenerObligatorio(id);
        usuario.HashContrasena = HashContrasena.Crear(nuevaContrasena);
        Guardar();
    }

    // ---------- Validaciones (públicas para poder probarlas) ----------

    public static string ValidarNombreUsuario(string? nombreUsuario)
    {
        string nombre = nombreUsuario?.Trim() ?? "";

        if (nombre.Length < 3 || nombre.Length > 50)
            throw new ArgumentException("El nombre de usuario debe tener entre 3 y 50 caracteres.");

        if (!nombre.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-'))
            throw new ArgumentException(
                "El nombre de usuario solo puede tener letras, números, punto, guion y guion bajo.");

        return nombre;
    }

    public static string ValidarEmail(string? email)
    {
        string correo = email?.Trim() ?? "";

        if (correo.Length == 0 || correo.Length > 254 ||
            !MailAddress.TryCreate(correo, out var direccion) || direccion.Address != correo)
            throw new ArgumentException("El email no tiene un formato válido.");

        return correo;
    }

    public static void ValidarContrasena(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinimaContrasena)
            throw new ArgumentException(
                $"La contraseña debe tener al menos {LongitudMinimaContrasena} caracteres.");

        if (!contrasena.Any(char.IsLetter) || !contrasena.Any(char.IsDigit))
            throw new ArgumentException("La contraseña debe combinar letras y números.");
    }

    // ---------- Auxiliares ----------

    private void ComprobarCambioDeRol(Usuario usuario, Rol nuevoRol, int idAdministradorActual)
    {
        if (usuario.Id == idAdministradorActual)
            throw new GenteFitException("No puedes cambiar tu propio rol.");

        if (usuario.Cliente != null || usuario.Monitor != null)
            throw new GenteFitException(
                "Esta cuenta está vinculada a una ficha de cliente o de monitor y su rol no se puede cambiar.");

        if (!RolesDePersonal.Contains(nuevoRol))
            throw new GenteFitException(
                "Una cuenta de personal no puede pasar a ser de cliente o de monitor.");

        if (usuario.Rol == Rol.ADMIN && usuario.Activo && EsUltimoAdministradorActivo(usuario.Id))
            throw new GenteFitException("Debe quedar al menos un administrador activo.");
    }

    private bool EsUltimoAdministradorActivo(int idUsuario) =>
        !_contexto.Usuarios.Any(u => u.Rol == Rol.ADMIN && u.Activo && u.Id != idUsuario);

    private void ComprobarQueNoExiste(string nombre, string correo, int? idExcluido)
    {
        if (_contexto.Usuarios.Any(u => u.NombreUsuario == nombre && u.Id != idExcluido))
            throw new GenteFitException($"Ya existe un usuario con el nombre «{nombre}».");

        if (_contexto.Usuarios.Any(u => u.Email == correo && u.Id != idExcluido))
            throw new GenteFitException($"Ya existe un usuario con el email «{correo}».");
    }

    private Usuario ObtenerObligatorio(int id) =>
        ObtenerPorId(id) ?? throw new GenteFitException("El usuario no existe.");

    private void Guardar()
    {
        try
        {
            _contexto.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            // Por ejemplo, otro administrador ha creado el mismo nombre a la vez (índice único).
            throw new GenteFitException(
                "No se han podido guardar los cambios. Comprueba que el nombre de usuario no esté repetido.", ex);
        }
    }
}
