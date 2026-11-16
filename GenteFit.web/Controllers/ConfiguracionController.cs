using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Persistencia.Xml;
using GenteFit.Web.Models.Admin;
using GenteFit.Web.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Web.Controllers;

// Configuración del centro y carga de actividades desde la fuente XML externa.
[Authorize(Roles = nameof(Rol.ADMIN))]
public class ConfiguracionController : Controller
{
    private readonly ConfiguracionCentroServicio _configuracion;
    private readonly ActividadRepositorio _actividades;
    private readonly ILogger<ConfiguracionController> _log;

    public ConfiguracionController(
        ConfiguracionCentroServicio configuracion,
        ActividadRepositorio actividades,
        ILogger<ConfiguracionController> log)
    {
        _configuracion = configuracion;
        _actividades = actividades;
        _log = log;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(CrearModelo(_configuracion.Obtener()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index([Bind(Prefix = "Centro")] ConfiguracionCentro centro)
    {
        if (centro.HoraCierre <= centro.HoraApertura)
            ModelState.AddModelError("Centro.HoraCierre", "La hora de cierre debe ser posterior a la de apertura.");

        if (!ModelState.IsValid)
            return View(CrearModelo(centro));

        try
        {
            centro.RutaFuenteXml = centro.RutaFuenteXml?.Trim().Trim('"') ?? "";
            _configuracion.Guardar(centro);
            TempData["Exito"] = "Configuración guardada.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogError(ex, "No se pudo guardar la configuración en {Ruta}.", _configuracion.RutaFichero);
            ModelState.AddModelError(string.Empty,
                $"No se ha podido guardar el fichero de configuración ({_configuracion.RutaFichero}). " +
                "Comprueba que la carpeta tiene permiso de escritura.");
            return View(CrearModelo(centro));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(CrearModelo(centro));
        }
    }

    // Lee la fuente XML configurada y da de alta las actividades que aún no existen.
    // La lectura nunca lanza excepciones: si la fuente no está disponible o está mal formada,
    // se informa al administrador y no se toca la base de datos.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarXml()
    {
        ConfiguracionCentro centro = _configuracion.Obtener();
        var resultado = new ResultadoImportacionViewModel();

        if (string.IsNullOrWhiteSpace(centro.RutaFuenteXml))
        {
            resultado.HayError = true;
            resultado.Error = "Primero indica y guarda la ruta de la fuente XML.";
        }
        else
        {
            ResultadoLecturaXml<Actividad> lectura = new ActividadXmlLector().Leer(centro.RutaFuenteXml);
            resultado.Avisos.AddRange(lectura.Avisos);
            resultado.Leidas = lectura.Elementos.Count;

            if (lectura.HayError)
            {
                resultado.HayError = true;
                resultado.Error = lectura.Error;
            }
            else
            {
                Importar(lectura.Elementos, resultado);
            }
        }

        var modelo = CrearModelo(centro);
        modelo.Importacion = resultado;
        return View(nameof(Index), modelo);
    }

    private void Importar(List<Actividad> leidas, ResultadoImportacionViewModel resultado)
    {
        var existentes = new HashSet<string>(
            _actividades.ObtenerTodas().Select(a => a.Nombre.Trim()),
            StringComparer.CurrentCultureIgnoreCase);

        foreach (Actividad actividad in leidas)
        {
            string nombre = actividad.Nombre.Trim();

            if (!existentes.Add(nombre))
            {
                resultado.Omitidas.Add($"{nombre} (ya existe)");
                continue;
            }

            if (string.IsNullOrWhiteSpace(actividad.Descripcion))
                actividad.Descripcion = "Actividad importada de la fuente XML.";

            try
            {
                _actividades.Crear(actividad);
                resultado.Importadas.Add(nombre);
            }
            catch (ArgumentException ex)
            {
                resultado.Omitidas.Add($"{nombre} ({ex.Message})");
            }
            catch (DbUpdateException ex)
            {
                _log.LogError(ex, "Error al guardar la actividad importada {Nombre}.", nombre);
                resultado.Omitidas.Add($"{nombre} (no se ha podido guardar)");
            }
        }
    }

    private ConfiguracionViewModel CrearModelo(ConfiguracionCentro centro) => new()
    {
        Centro = centro,
        RutaFicheroConfiguracion = _configuracion.RutaFichero,
        FuenteXmlExiste = !string.IsNullOrWhiteSpace(centro.RutaFuenteXml) && System.IO.File.Exists(centro.RutaFuenteXml)
    };
}
