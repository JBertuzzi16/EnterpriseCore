using Microsoft.AspNetCore.Mvc;
using GenteFit.Persistencia.Repositorios;
using Microsoft.AspNetCore.Authorization;
using GenteFit.web.Models;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;


namespace GenteFit.web.Controllers;

[Authorize(Roles = "RECEPCIONISTA,ADMIN")]

public class RecepcionController : Controller
{
    private readonly ClienteRepositorio _repositorio;

    public RecepcionController(ClienteRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var clientes = _repositorio.ObtenerTodos();
        return View(clientes);
    }

    [HttpGet]
    public IActionResult Ficha (int id)
    {
        var cliente = _repositorio.ObtenerPorId (id);

        if (cliente == null)
            return NotFound();

        return View(cliente);
    }

    [HttpGet]
    public IActionResult Alta()
    {
        return View(new AltaClienteViewModel());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Alta (AltaClienteViewModel modelo)
    {
        if (modelo.FechaNacimiento is DateTime fecha && fecha > DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(modelo.FechaNacimiento),
                ("La fecha de nacimiento debe ser menor que hoy"));
        }

        if(!ModelState.IsValid || modelo.FechaNacimiento is not DateTime nacimiento)
        {
            return View(modelo);

        }
        var cliente = new Cliente
        {
            Nombre = modelo.Nombre.Trim(),
            Apellidos = modelo.Apellidos.Trim(),
            Dni = modelo.Dni.Trim(),
            Email = modelo.Email.Trim(),
            Telefono = modelo.Telefono?.Trim() ?? "",
            FechaNacimiento = nacimiento
        };

        try
        {
            _repositorio.Crear(cliente, modelo.Contrasena);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
                return View(modelo);
        }
        catch(DbUpdateException)
        {
            ModelState.AddModelError("", "No se ha podiso guardas el cliente");
            return View(modelo);
        }

        return RedirectToAction(nameof(Ficha), new { id = cliente.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Baja(int id)
    {
        try
        {
            if (!_repositorio.DarDeBaja(id))
                return NotFound();

            TempData["BajaCorrecta"] = "El cliente se ha dado de baja.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorBaja"] = ex.Message;
        }
        catch (DbUpdateException)
        {
            TempData["ErrorBaja"] = "No se ha podido guardar la baja.";
        }

        return RedirectToAction(nameof(Ficha), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Reactivar(int id)
    {
        try
        {
            if (!_repositorio.Reactivar(id))
                return NotFound();

            TempData["BajaCorrecta"] = "El cliente se ha reactivado.";
        }
        catch (DbUpdateException)
        {
            TempData["ErrorBaja"] = "No se ha podido reactivar al cliente.";
        }

        return RedirectToAction(nameof(Ficha), new { id });
    }


}
