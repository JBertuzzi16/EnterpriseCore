using GenteFit.Persistencia.Repositorios;
using Microsoft.AspNetCore.Mvc;

namespace GenteFit.Web.ViewComponents;

public class ContadorReservasViewComponent : ViewComponent
{
    private readonly ReservaRepositorio _reservas;
    private readonly ILogger<ContadorReservasViewComponent> _logger;

    public ContadorReservasViewComponent(
        ReservaRepositorio reservas,
        ILogger<ContadorReservasViewComponent> logger)
    {
        _reservas = reservas;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        string? valorClaim = HttpContext.User.FindFirst("ClienteId")?.Value;

        if (!int.TryParse(valorClaim, out int clienteId) || clienteId <= 0)
        {
            _logger.LogWarning("No se encontró un ClienteId válido en la sesión del cliente.");
            return Content("—");
        }

        try
        {
            int cantidad = await _reservas.ContarActivasClienteAsync(
                clienteId, HttpContext.RequestAborted);
            return Content(cantidad.ToString());
        }
        catch (Exception ex) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            // No impedimos que el usuario navegue por un fallo temporal de SQL Server.
            _logger.LogError(ex, "Error al consultar el número de reservas del cliente.");
            return Content("—");
        }
    }
}
