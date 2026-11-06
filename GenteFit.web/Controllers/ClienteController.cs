using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenteFit.Web.Controllers;

[Authorize(Roles = "CLIENTE")]
public class ClienteController : Controller
{
    // En el paso 4 conectaremos estas pantallas con los repositorios.
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult MisReservas() => View();

    [HttpGet]
    public IActionResult MiPerfil() => View();
}
