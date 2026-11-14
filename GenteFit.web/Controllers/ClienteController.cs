using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenteFit.Web.Controllers;

[Authorize(Roles = "CLIENTE")]
public class ClienteController : Controller
{
    private readonly SesionRepositorio _sesiones;
    private readonly ReservaRepositorio _reservas;
    private readonly ClienteRepositorio _clientes;
    private readonly ILogger<ClienteController> _logger;

    public ClienteController(
        SesionRepositorio sesiones,
        ReservaRepositorio reservas,
        ClienteRepositorio clientes,
        ILogger<ClienteController> logger)
    {
        _sesiones = sesiones;
        _reservas = reservas;
        _clientes = clientes;
        _logger = logger;
    }
    // Obtenemos el cliente asociado a la sesión.
    private int? ObtenerClienteId()
    {
        string? valor = User.FindFirst("ClienteId")?.Value;

        return int.TryParse(valor, out int clienteId)
            && clienteId > 0
            ? clienteId
            : null;
    }
    // Perfil del cliente autenticado.
    [HttpGet]
    public async Task<IActionResult> MiPerfil(
        CancellationToken cancelacion)
    {
        int? clienteId = ObtenerClienteId();

        if (!clienteId.HasValue)
            return Forbid();

        var modelo = new PerfilClienteViewModel();

        try
        {
            modelo.Perfil =
                await _clientes.ObtenerPerfilAsync(
                    clienteId.Value,
                    cancelacion);

            if (modelo.Perfil == null)
            {
                modelo.MensajeError =
                    "No hemos encontrado tu ficha de cliente. " +
                    "Contacta con el personal del centro.";
            }
        }
        catch (Exception ex)
            when (!cancelacion.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "Error al consultar el perfil del cliente {ClienteId}.",
                clienteId.Value);

            modelo.MensajeError =
                "No podemos cargar tu perfil ahora. " +
                "Inténtalo de nuevo dentro de unos minutos.";
        }

        return View(modelo);
    }
    // Catálogo de actividades.
    [HttpGet]
    public async Task<IActionResult> Index(
        string? nombre,
        DateTime? fecha,
        NivelIntensidad? intensidad,
        CancellationToken cancelacion)
    {
        int? clienteId = ObtenerClienteId();

        if (!clienteId.HasValue)
            return Forbid();

        var modelo = new CatalogoClienteViewModel
        {
            Nombre = nombre?.Trim(),
            Fecha = fecha,
            Intensidad = intensidad
        };

        if (!ModelState.IsValid ||
            (intensidad.HasValue &&
             !Enum.IsDefined(intensidad.Value)))
        {
            modelo.MensajeError =
                "Los filtros introducidos no son válidos.";

            return View(modelo);
        }

        if (modelo.Nombre?.Length > 80)
        {
            modelo.MensajeError =
                "La búsqueda no puede superar los 80 caracteres.";

            return View(modelo);
        }

        try
        {
            modelo.Sesiones =
                await _sesiones.ObtenerProximasAsync(
                    modelo.Nombre,
                    modelo.Fecha,
                    modelo.Intensidad,
                    cancelacion);

            // Consultamos qué sesiones ya tiene
            // reservadas el cliente autenticado.
            modelo.EstadosCliente =
                await _reservas.ObtenerEstadosClienteAsync(
                    clienteId.Value,
                    modelo.Sesiones.Select(s => s.Id),
                    cancelacion);
        }
        catch (Exception ex)
            when (!cancelacion.IsCancellationRequested)
        {
            _logger.LogError(ex,
                "Error al consultar el catálogo del cliente.");

            modelo.Sesiones.Clear();
            modelo.EstadosCliente.Clear();

            modelo.MensajeError =
                "No podemos cargar las actividades ahora. " +
                "Inténtalo de nuevo dentro de unos minutos.";
        }

        return View(modelo);
    }

    // Crear una reserva desde una tarjeta.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reservar(
        int sesionId,
        string? nombre,
        DateTime? fecha,
        NivelIntensidad? intensidad,
        CancellationToken cancelacion)
    {
        int? clienteId = ObtenerClienteId();

        if (!clienteId.HasValue)
            return Forbid();

        if (sesionId <= 0)
        {
            TempData["Error"] =
                "La sesión seleccionada no es válida.";

            return VolverCatalogo(nombre, fecha, intensidad);
        }

        try
        {
            Reserva reserva = await _reservas.CrearAsync(
                clienteId.Value,
                sesionId,
                cancelacion);

            if (reserva.Estado == EstadoReserva.CONFIRMADA)
            {
                TempData["Exito"] =
                    "¡Reserva confirmada! Ya tienes tu plaza.";
            }
            else
            {
                TempData["Exito"] =
                    "La sesión está completa. " +
                    "Te hemos añadido a la lista de espera.";
            }
        }
        catch (GenteFitException ex)
        {
            // Mostramos un error de negocio comprensible.
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
            when (!cancelacion.IsCancellationRequested)
        {
            _logger.LogError(ex,
                "Error al reservar la sesión {SesionId} " +
                "para el cliente {ClienteId}.",
                sesionId, clienteId.Value);

            TempData["Error"] =
                "No hemos podido procesar la reserva. " +
                "Consulta Mis Reservas antes de intentarlo " +
                "de nuevo, por si se hubiera guardado.";
        }

        // Redirigimos para evitar repetir el POST
        // si el usuario actualiza la página.
        return VolverCatalogo(nombre, fecha, intensidad);
    }

    // Anular una reserva desde el catálogo.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(
        int sesionId,
        string? nombre,
        DateTime? fecha,
        NivelIntensidad? intensidad,
        bool desdeMisReservas,
        CancellationToken cancelacion)
    {
        int? clienteId = ObtenerClienteId();

        if (!clienteId.HasValue)
            return Forbid();

        if (sesionId <= 0)
        {
            TempData["Error"] =
                "La sesión seleccionada no es válida.";
        }
        else
        {
            try
            {
                ResultadoAnulacion resultado =
                    await _reservas.AnularPorSesionAsync(
                        clienteId.Value,
                        sesionId,
                        cancelacion);

                TempData["Exito"] = resultado.HayPromocion
                    ? "Tu reserva se ha anulado. " +
                      "La plaza ha pasado al primer cliente en espera."
                    : "Tu reserva se ha anulado correctamente.";
            }
            catch (GenteFitException ex)
            {
                TempData["Error"] = ex.Message;
            }
            catch (Exception ex)
                when (!cancelacion.IsCancellationRequested)
            {
                _logger.LogError(
                    ex,
                    "Error al anular la sesión {SesionId} " +
                    "del cliente {ClienteId}.",
                    sesionId,
                    clienteId.Value);

                TempData["Error"] =
                    "No hemos podido confirmar la anulación. " +
                    "Comprueba el estado de tu reserva.";
            }
        }

        // Solo permitimos volver a dos destinos conocidos.
        // No utilizamos URLs recibidas del formulario.
        if (desdeMisReservas)
            return RedirectToAction(nameof(MisReservas));

        return VolverCatalogo(nombre, fecha, intensidad);
    }

    // Conservamos los filtros tras reservar.
    private IActionResult VolverCatalogo(
        string? nombre,
        DateTime? fecha,
        NivelIntensidad? intensidad)
    {
        return RedirectToAction(nameof(Index), new
        {
            nombre,
            fecha = fecha?.ToString("yyyy-MM-dd"),
            intensidad = intensidad?.ToString()
        });
    }

    [HttpGet]
    public async Task<IActionResult> MisReservas(
        CancellationToken cancelacion)
    {
        int? clienteId = ObtenerClienteId();

        if (!clienteId.HasValue)
            return Forbid();

        var modelo = new MisReservasClienteViewModel();

        try
        {
            modelo.Reservas =
                await _reservas.ObtenerReservasClienteAsync(
                    clienteId.Value,
                    cancelacion);
        }
        catch (Exception ex)
            when (!cancelacion.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "Error al consultar las reservas del cliente {ClienteId}.",
                clienteId.Value);

            modelo.MensajeError =
                "No podemos consultar tus reservas ahora. " +
                "Inténtalo de nuevo dentro de unos minutos.";
        }

        return View(modelo);
    }
}
