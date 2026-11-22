namespace GenteFit.web.Models;

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

public class AsistenciaViewModel
{
    public int SesionId { get; set; }
    [ValidateNever]
    public string NombreActividad { get; set; } = "";

    public DateTime FechaHoraInicio { get; set; }

    public List<AsistenciaClienteViewModel> Clientes { get; set; } = new();
}

public class AsistenciaClienteViewModel
{
    public int ReservaId { get; set; }
    [ValidateNever]
    public string NombreCompleto { get; set; } = "";

    public bool? Asistio { get; set; }
}