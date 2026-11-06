
using GenteFit.web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GenteFit.web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            // Identificador para relacionar el error
            // mostrado al usuario con el registro técnico.
            string referencia = Activity.Current?.Id
                ?? HttpContext.TraceIdentifier;

            // Recuperamos la excepción que provocó
            // la redirección a esta página.
            var detalle = HttpContext.Features
                .Get<IExceptionHandlerPathFeature>();

            if (detalle?.Error != null)
            {
                // Los detalles se registran en el servidor.
                // Nunca se muestran en la interfaz.
                _logger.LogError(
                    detalle.Error,
                    "Error inesperado en {Ruta}. Referencia: {Referencia}",
                    detalle.Path,
                    referencia);
            }

            return View(new ErrorViewModel
            {
                RequestId = referencia
            });
        }
    }
}
