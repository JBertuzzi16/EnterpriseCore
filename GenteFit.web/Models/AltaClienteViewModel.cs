using Microsoft.Identity.Client;
using System.ComponentModel.DataAnnotations;

namespace GenteFit.web.Models;


public class AltaClienteViewModel
{
    [Required]

    public string Nombre { get; set; } = "";

    [Required]
    public string Apellidos { get; set; } = "";

    [Required]

    public string Dni { get; set; } = "";

    [Required, EmailAddress]

    public string Email { get; set; } = "";

    public string? Telefono { get; set; }

    [Required, DataType(DataType.Date)]

    public DateTime? FechaNacimiento {  get; set; }

    [Required, DataType(DataType.Password)]

    public string Contrasena { get; set; } = "";




}
