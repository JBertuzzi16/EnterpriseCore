using GenteFit.Persistencia.Repositorios;
using GenteFit.web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using GenteFit.Persistencia.Entidades;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.web.Controllers;

[Authorize(Roles = "ENCARGADO,ADMIN")]
public class SesionesController : Controller
{
    private readonly SesionRepositorio _repositorio;

    public SesionesController(SesionRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    [HttpGet]
    public IActionResult Crear()
    {
        var modelo = new CrearSesionViewModel();

        CargarOpciones(modelo);

        return View(modelo);
    }

    private void CargarOpciones(CrearSesionViewModel modelo)
    {
        modelo.Actividades = _repositorio.ObtenerActividadesActivas()
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = a.Nombre
            })
            .ToList();

        modelo.Salas = _repositorio.ObtenerSalasActivas()
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Nombre
            })
            .ToList();

        modelo.Monitores = _repositorio.ObtenerMonitoresActivos()
            .Select(m => new SelectListItem
            {
                Value = m.Id.ToString(),
                Text = $"{m.Nombre} {m.Apellidos}"
            })
            .ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Crear(CrearSesionViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            CargarOpciones(modelo);
            return View(modelo);
        }

        var sesion = new Sesion
        {
            ActividadId = modelo.ActividadId,
            SalaId = modelo.SalaId,
            MonitorId = modelo.MonitorId,
            FechaHoraInicio = modelo.FechaHoraInicio!.Value,
            FechaHoraFin = modelo.FechaHoraFin!.Value
        };

        try
        {
            _repositorio.Crear(sesion);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
            CargarOpciones(modelo);
            return View(modelo);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                "",
                "No se ha podido guardar la sesión. Inténtalo de nuevo.");

            CargarOpciones(modelo);
            return View(modelo);
        }

        TempData["SesionCreada"] = "La sesión se ha creado correctamente.";

        return RedirectToAction(nameof(Crear));
    }
}
