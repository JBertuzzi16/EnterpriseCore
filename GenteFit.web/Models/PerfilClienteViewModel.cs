using GenteFit.Persistencia.Repositorios;

namespace GenteFit.Web.Models;

public class PerfilClienteViewModel
{
    public ClientePerfilResumen? Perfil { get; set; }

    public string? MensajeError { get; set; }
}