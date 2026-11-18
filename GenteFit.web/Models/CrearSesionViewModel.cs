using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GenteFit.web.Models;

public class CrearSesionViewModel
{
    [Range(1, int.MaxValue)]
    public int ActividadId {  get; set; }

    [Range(1, int.MaxValue)]
    public int SalaId { get; set; }

    [Range(1, int.MaxValue)]
    public int MonitorId { get; set; }

    [Required(ErrorMessage = "Indica la fecha y hora de inicio.")]
    public DateTime? FechaHoraInicio { get; set; }

    [Required(ErrorMessage = "Indica la fecha y hora de fin.")]
    public DateTime? FechaHoraFin {  get; set; }

    public List<SelectListItem> Actividades { get; set; } = new();
    public List<SelectListItem> Salas { get; set; } = new();

    public List<SelectListItem> Monitores { get; set; } = new();

}
