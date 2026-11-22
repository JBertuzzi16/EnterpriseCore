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

    [HttpGet]
    public IActionResult Index()
    {
        var sesiones = _repositorio.ObtenerTodas();

        return View(sesiones);
    }

    [HttpGet]
    public IActionResult Editar(int id)
    {
        var sesion = _repositorio.ObtenerPorId(id);

        if (sesion == null)
            return NotFound();

        if (sesion.Anulada || sesion.FechaHoraInicio <= DateTime.Now)
        {
            return BadRequest(
                "Solo se pueden editar sesiones futuras que no estén anuladas.");
        }

        var modelo = new CrearSesionViewModel
        {
            Id = sesion.Id,
            ActividadId = sesion.ActividadId,
            SalaId = sesion.SalaId,
            MonitorId = sesion.MonitorId,
            FechaHoraInicio = sesion.FechaHoraInicio,
            FechaHoraFin = sesion.FechaHoraFin
        };

        CargarOpciones(modelo);

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(CrearSesionViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            CargarOpciones(modelo);
            return View(modelo);
        }

        var datos = new Sesion
        {
            Id = modelo.Id,
            ActividadId = modelo.ActividadId,
            SalaId = modelo.SalaId,
            MonitorId = modelo.MonitorId,
            FechaHoraInicio = modelo.FechaHoraInicio!.Value,
            FechaHoraFin = modelo.FechaHoraFin!.Value
        };

        try
        {
            if (!_repositorio.Actualizar(datos))
                return NotFound();
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
                "", "No se han podido guardar los cambios.");

            CargarOpciones(modelo);
            return View(modelo);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cancelar(int id)
    {
        try
        {
            if (!_repositorio.Cancelar(id))
                return NotFound();

            TempData["Mensaje"] = "La sesión se ha cancelado.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "No se ha podido cancelar la sesión.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Asistencia(int id)
    {
        var sesion = _repositorio.ObtenerPorId(id);

        if (sesion == null)
            return NotFound();

        if (sesion.Anulada)
            return BadRequest(
                "No se puede registrar asistencia en una sesión anulada.");

        var reservas = _repositorio.ObtenerReservasConfirmadas(id);

        var modelo = new AsistenciaViewModel
        {
            SesionId = sesion.Id,
            NombreActividad = sesion.Actividad.Nombre,
            FechaHoraInicio = sesion.FechaHoraInicio,
            Clientes = reservas.Select(r => new AsistenciaClienteViewModel
            {
                ReservaId = r.Id,
                NombreCompleto = $"{r.Cliente.Nombre} {r.Cliente.Apellidos}",
                Asistio = r.Asistio
            }).ToList()
        };

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Asistencia(AsistenciaViewModel modelo)
    {
        if (!ModelState.IsValid || modelo.Clientes == null)
        {
            TempData["ErrorAsistencia"] =
                "Los datos enviados no son válidos. Revisa la asistencia.";

            return RedirectToAction(
                nameof(Asistencia), new { id = modelo.SesionId });
        }

        try
        {
            var asistencias = modelo.Clientes.ToDictionary(
                c => c.ReservaId,
                c => c.Asistio);

            if (!_repositorio.GuardarAsistencia(
                modelo.SesionId, asistencias))
            {
                return NotFound();
            }

            TempData["AsistenciaGuardada"] =
                "La asistencia se ha guardado correctamente.";
        }
        catch (ArgumentException ex)
        {
            TempData["ErrorAsistencia"] = ex.Message;
        }
        catch (DbUpdateException)
        {
            TempData["ErrorAsistencia"] =
                "No se ha podido guardar la asistencia.";
        }

        return RedirectToAction(
            nameof(Asistencia), new { id = modelo.SesionId });
    }
}
