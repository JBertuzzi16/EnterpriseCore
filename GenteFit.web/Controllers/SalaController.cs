using GenteFit.Persistencia.Repositorios;
using Microsoft.AspNetCore.Mvc;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Web.Controllers;

public class SalasController : Controller
{
    private readonly SalaRepositorio _repositorio;

    public SalasController(SalaRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Crear(string? nombre, string? ubicacion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            ModelState.AddModelError("", "El nombre es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(ubicacion))
        {
            ModelState.AddModelError("", "La ubicación es obligatoria.");
        }

        if (!ModelState.IsValid)
        {
            return View();
        }

        Sala sala = new Sala
        {
            Nombre = nombre!.Trim(),
            Ubicacion = ubicacion!.Trim()
        };

        try
        {
            _repositorio.Crear(sala);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                "", "No se ha podido guardar la sala. Inténtalo de nuevo.");

            return View();
        }

        TempData["Mensaje"] = $"Sala guardada con el identificador {sala.Id}.";

        return RedirectToAction(nameof(Crear));
    }

    [HttpGet]
    public IActionResult Index()
    {
        var salas = _repositorio.ObtenerTodas();

        return View(salas);
    }

    [HttpGet]
    public IActionResult Editar(int id)
    {
        Sala? sala = _repositorio.ObtenerPorId(id);

        if (sala == null)
            return NotFound();

        return View(sala);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(int id, string? nombre, string? ubicacion)
    {
        Sala? sala = _repositorio.ObtenerPorId(id);

        if (sala == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(nombre))
            ModelState.AddModelError("Nombre", "El nombre es obligatorio.");

        if (string.IsNullOrWhiteSpace(ubicacion))
            ModelState.AddModelError("Ubicacion", "La ubicación es obligatoria.");

        if (!ModelState.IsValid)
            return View(sala);

        try
        {
            bool actualizada = _repositorio.Actualizar(id, nombre!, ubicacion!);

            if (!actualizada)
                return NotFound();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                "",
                "No se han podido guardar los cambios. Inténtalo de nuevo.");

            return View(sala);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Desactivar(int id)
    {
        try
        {
            bool desactivada = _repositorio.Desactivar(id);

            if (!desactivada)
                return NotFound();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "No se ha podido desactivar la sala.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Activar(int id)
    {
        try
        {
            bool activada = _repositorio.Activar(id);

            if (!activada)
                return NotFound();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "No se ha podido activar la sala.";
        }

        return RedirectToAction(nameof(Index));
    }

}