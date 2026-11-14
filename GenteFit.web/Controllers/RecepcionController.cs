using Microsoft.AspNetCore.Mvc;
using GenteFit.Persistencia.Repositorios;

namespace GenteFit.web.Controllers;

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
}
