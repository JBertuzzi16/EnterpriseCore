using System.Data.Common;
using System.Security.Claims;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenteFit.Web.Controllers;

public class CuentaController : Controller
{
    private readonly UsuarioRepositorio _usuarios;
    private readonly ILogger<CuentaController> _log;

    public CuentaController(UsuarioRepositorio usuarios, ILogger<CuentaController> log)
    {
        _usuarios = usuarios;
        _log = log;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirigirSegunRol(User.FindFirstValue(ClaimTypes.Role));

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        Usuario? usuario;

        try
        {
            usuario = _usuarios.ValidarCredenciales(modelo.Email, modelo.Contrasena);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            // Base de datos apagada, sin red, cadena de conexión incorrecta...
            _log.LogError(ex, "No se pudo consultar la base de datos durante el inicio de sesión.");
            ModelState.AddModelError(string.Empty,
                "No se puede conectar con el sistema en este momento. Inténtalo de nuevo en unos minutos.");
            modelo.Contrasena = string.Empty;
            return View(modelo);
        }

        if (usuario == null)
        {
            ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
            modelo.Contrasena = string.Empty;
            return View(modelo);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NombreUsuario),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Role, usuario.Rol.ToString())
        };

        if (usuario.Cliente != null)
            claims.Add(new Claim("ClienteId", usuario.Cliente.Id.ToString()));

        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidad));

        if (!string.IsNullOrEmpty(modelo.ReturnUrl) && Url.IsLocalUrl(modelo.ReturnUrl))
            return LocalRedirect(modelo.ReturnUrl);

        return RedirigirSegunRol(usuario.Rol.ToString());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
              CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]

    public IActionResult AccesoDenegado()
    {
        return View();
    }

    // Cada rol tiene su portal (diseño del Producto 1). De momento todos aterrizan en Home;
    // cuando existan las pantallas de cada portal se cambia aquí el destino.
    private IActionResult RedirigirSegunRol(string? rol)
    {
        return rol switch
        {
            nameof(Rol.CLIENTE)       => RedirectToAction("Index", "Home"),   // -> catálogo de actividades
            nameof(Rol.RECEPCIONISTA) => RedirectToAction("Index", "Home"),   // -> gestor de clientes
            nameof(Rol.ENCARGADO)     => RedirectToAction("Index", "Home"),   // -> estado de reservas
            nameof(Rol.ADMIN)         => RedirectToAction("Index", "Home"),   // -> panel de administración
            _                         => RedirectToAction("Index", "Home")
        };
    }
}
