using GenteFit.Persistencia.Repositorios;
using Microsoft.AspNetCore.Mvc;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Web.Controllers;

public class ActividadesController : Controller
{
    private readonly ActividadRepositorio _repositorio;

    public ActividadesController(ActividadRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var actividades = _repositorio.ObtenerTodas();

        return View(actividades);
    }
    [HttpGet]
    public IActionResult Crear()
    {
        return View(new Actividad());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Crear(
    string? nombre,
    string? descripcion,
    int duracionMinutos,
    NivelIntensidad intensidad)
    {
        Actividad actividad = new Actividad
        {
            Nombre = nombre?.Trim() ?? "",
            Descripcion = descripcion?.Trim() ?? "",
            DuracionMinutos = duracionMinutos,
            Intensidad = intensidad
        };

        if (!ModelState.IsValid)
            return View(actividad);

        try
        {
            _repositorio.Crear(actividad);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(actividad);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                "",
                "No se ha podido guardar la actividad.");

            return View(actividad);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Editar(int id)
    {
        Actividad? actividad = _repositorio.ObtenerPorId(id);

        if (actividad == null)
            return NotFound();

        return View(actividad);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(
    int id,
    string? nombre,
    string? descripcion,
    int duracionMinutos,
    NivelIntensidad intensidad)
    {
        Actividad? actividad = _repositorio.ObtenerPorId(id);

        if (actividad == null)
            return NotFound();

        if (!ModelState.IsValid)
            return View(actividad);

        try
        {
            bool actualizada = _repositorio.Actualizar(
                id,
                nombre?.Trim() ?? "",
                descripcion?.Trim() ?? "",
                duracionMinutos,
                intensidad);

            if (!actualizada)
                return NotFound();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(actividad);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                "",
                "No se han podido guardar los cambios.");

            return View(actividad);
        }

        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Desactivar(int id)
    {
        try
        {
            if (!_repositorio.Desactivar(id))
                return NotFound();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "No se ha podido desactivar la actividad.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Activar(int id)
    {
        try
        {
            if (!_repositorio.Activar(id))
                return NotFound();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "No se ha podido activar la actividad.";
        }

        return RedirectToAction(nameof(Index));
    }
}