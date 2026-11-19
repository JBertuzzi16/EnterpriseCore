using System.Data.Common;
using System.Security.Claims;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Web.Models.Admin;
using GenteFit.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenteFit.Web.Controllers;

// Portal del administrador: panel de inicio y gestión de usuarios y roles.
[Authorize(Roles = nameof(Rol.ADMIN))]
public class AdminController : Controller
{
    private readonly AdminUsuarioRepositorio _usuarios;
    private readonly EstadisticasRepositorio _estadisticas;
    private readonly ConfiguracionCentroServicio _configuracion;
    private readonly ILogger<AdminController> _log;

    public AdminController(
        AdminUsuarioRepositorio usuarios,
        EstadisticasRepositorio estadisticas,
        ConfiguracionCentroServicio configuracion,
        ILogger<AdminController> log)
    {
        _usuarios = usuarios;
        _estadisticas = estadisticas;
        _configuracion = configuracion;
        _log = log;
    }

    private int IdUsuarioActual =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0;

    // ---------- Panel ----------

    [HttpGet]
    public IActionResult Index()
    {
        DateTime hoy = DateTime.Today;

        var modelo = new PanelAdminViewModel
        {
            NombreCentro = _configuracion.Obtener().NombreCentro,
            ResumenSemana = _estadisticas.Resumen(hoy.AddDays(-6), hoy),
            UsuariosPorRol = _usuarios.ContarPorRol(),
            ProximasSesiones = _estadisticas
                .OcupacionPorSesion(hoy, hoy.AddDays(7))
                .Where(s => s.Inicio >= DateTime.Now)
                .Take(8)
                .ToList()
        };

        return View(modelo);
    }

    // ---------- Usuarios ----------

    [HttpGet]
    public IActionResult Usuarios(string? texto, Rol? rol, bool? activo)
    {
        return View(new ListaUsuariosViewModel
        {
            Usuarios = _usuarios.Listar(texto, rol, activo),
            Texto = texto,
            Rol = rol,
            Activo = activo,
            IdUsuarioActual = IdUsuarioActual
        });
    }

    [HttpGet]
    public IActionResult CrearUsuario()
    {
        return View("FormularioUsuario", new UsuarioFormularioViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CrearUsuario(UsuarioFormularioViewModel modelo)
    {
        if (string.IsNullOrEmpty(modelo.Contrasena))
            ModelState.AddModelError(nameof(modelo.Contrasena), "La contraseña inicial es obligatoria.");

        if (!ModelState.IsValid)
            return View("FormularioUsuario", modelo);

        try
        {
            Usuario usuario = _usuarios.Crear(modelo.NombreUsuario, modelo.Email, modelo.Contrasena!, modelo.Rol);
            _log.LogInformation("El administrador {Admin} ha creado el usuario {Usuario} ({Rol}).",
                User.Identity?.Name, usuario.NombreUsuario, usuario.Rol);

            TempData["Exito"] = $"Usuario «{usuario.NombreUsuario}» creado con el rol {NombreRol(usuario.Rol)}.";
            return RedirectToAction(nameof(Usuarios));
        }
        catch (Exception ex) when (ex is ArgumentException or GenteFitException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("FormularioUsuario", modelo);
        }
    }

    [HttpGet]
    public IActionResult EditarUsuario(int id)
    {
        Usuario? usuario = _usuarios.ObtenerPorId(id);
        if (usuario == null)
            return NotFound();

        return View("FormularioUsuario", new UsuarioFormularioViewModel
        {
            Id = usuario.Id,
            NombreUsuario = usuario.NombreUsuario,
            Email = usuario.Email,
            Rol = usuario.Rol,
            TieneFicha = usuario.Cliente != null || usuario.Monitor != null,
            EsUsuarioActual = usuario.Id == IdUsuarioActual
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditarUsuario(int id, UsuarioFormularioViewModel modelo)
    {
        // Al editar no se cambia la contraseña (hay una pantalla propia para eso).
        ModelState.Remove(nameof(modelo.Contrasena));
        ModelState.Remove(nameof(modelo.ConfirmarContrasena));
        modelo.Id = id;

        Usuario? usuario = _usuarios.ObtenerPorId(id);
        if (usuario == null)
            return NotFound();

        modelo.TieneFicha = usuario.Cliente != null || usuario.Monitor != null;
        modelo.EsUsuarioActual = id == IdUsuarioActual;

        if (!ModelState.IsValid)
            return View("FormularioUsuario", modelo);

        try
        {
            _usuarios.Editar(id, modelo.NombreUsuario, modelo.Email, modelo.Rol, IdUsuarioActual);
            TempData["Exito"] = $"Cambios guardados en «{modelo.NombreUsuario.Trim()}».";
            return RedirectToAction(nameof(Usuarios));
        }
        catch (Exception ex) when (ex is ArgumentException or GenteFitException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("FormularioUsuario", modelo);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Desactivar(int id)
    {
        return EjecutarAccion(() => _usuarios.Desactivar(id, IdUsuarioActual), "Cuenta desactivada: ya no puede iniciar sesión.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Activar(int id)
    {
        return EjecutarAccion(() => _usuarios.Activar(id), "Cuenta activada.");
    }

    [HttpGet]
    public IActionResult RestablecerContrasena(int id)
    {
        Usuario? usuario = _usuarios.ObtenerPorId(id);
        if (usuario == null)
            return NotFound();

        return View(new RestablecerContrasenaViewModel { Id = id, NombreUsuario = usuario.NombreUsuario });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RestablecerContrasena(RestablecerContrasenaViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        try
        {
            _usuarios.RestablecerContrasena(modelo.Id, modelo.Contrasena);
            TempData["Exito"] = $"Contraseña de «{modelo.NombreUsuario}» cambiada.";
            return RedirectToAction(nameof(Usuarios));
        }
        catch (Exception ex) when (ex is ArgumentException or GenteFitException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(modelo);
        }
    }

    // ---------- Auxiliares ----------

    private IActionResult EjecutarAccion(Action accion, string mensajeExito)
    {
        try
        {
            accion();
            TempData["Exito"] = mensajeExito;
        }
        catch (GenteFitException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (DbException ex)
        {
            _log.LogError(ex, "Error de base de datos en la gestión de usuarios.");
            TempData["Error"] = "No se puede conectar con la base de datos. Inténtalo de nuevo en unos minutos.";
        }

        return RedirectToAction(nameof(Usuarios));
    }

    public static string NombreRol(Rol rol) => rol switch
    {
        Rol.ADMIN => "Administrador",
        Rol.RECEPCIONISTA => "Recepcionista",
        Rol.ENCARGADO => "Encargado",
        Rol.CLIENTE => "Cliente",
        Rol.MONITOR => "Monitor",
        _ => rol.ToString()
    };
}
