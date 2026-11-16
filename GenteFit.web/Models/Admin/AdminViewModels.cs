using System.ComponentModel.DataAnnotations;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Informes;
using GenteFit.Web.Servicios;

namespace GenteFit.Web.Models.Admin;

public class PanelAdminViewModel
{
    public ResumenEstadisticas ResumenSemana { get; set; } = null!;
    public Dictionary<Rol, int> UsuariosPorRol { get; set; } = new();
    public List<OcupacionSesion> ProximasSesiones { get; set; } = new();
    public string NombreCentro { get; set; } = "GenteFit";
}

public class ListaUsuariosViewModel
{
    public List<Usuario> Usuarios { get; set; } = new();
    public string? Texto { get; set; }
    public Rol? Rol { get; set; }
    public bool? Activo { get; set; }
    public int IdUsuarioActual { get; set; }
}

public class UsuarioFormularioViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Entre 3 y 50 caracteres.")]
    [Display(Name = "Nombre de usuario")]
    public string NombreUsuario { get; set; } = "";

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    [Display(Name = "Email (se usa para iniciar sesión)")]
    public string Email { get; set; } = "";

    [Display(Name = "Rol")]
    public Rol Rol { get; set; } = Rol.RECEPCIONISTA;

    // Solo al crear.
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña inicial")]
    public string? Contrasena { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Repite la contraseña")]
    [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    public string? ConfirmarContrasena { get; set; }

    // Datos informativos al editar.
    public bool TieneFicha { get; set; }
    public bool EsUsuarioActual { get; set; }
    public bool EsNuevo => Id == null;
}

public class RestablecerContrasenaViewModel
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = "";

    [Required(ErrorMessage = "Escribe la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string Contrasena { get; set; } = "";

    [Required(ErrorMessage = "Repite la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Repite la contraseña")]
    [Compare(nameof(Contrasena), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarContrasena { get; set; } = "";
}

public class InformesViewModel
{
    [DataType(DataType.Date)]
    public DateTime Desde { get; set; }

    [DataType(DataType.Date)]
    public DateTime Hasta { get; set; }

    public int Anio { get; set; }

    public ResumenEstadisticas Resumen { get; set; } = null!;
    public List<RankingActividad> Actividades { get; set; } = new();
    public List<OcupacionSala> Salas { get; set; } = new();
    public List<OcupacionSesion> Sesiones { get; set; } = new();
    public List<AltasMes> AltasPorMes { get; set; } = new();
    public ConfiguracionCentro Centro { get; set; } = new();
}

public class ConfiguracionViewModel
{
    public ConfiguracionCentro Centro { get; set; } = new();
    public string RutaFicheroConfiguracion { get; set; } = "";
    public bool FuenteXmlExiste { get; set; }
    public ResultadoImportacionViewModel? Importacion { get; set; }
}

public class ResultadoImportacionViewModel
{
    public bool HayError { get; set; }
    public string? Error { get; set; }
    public int Leidas { get; set; }
    public List<string> Importadas { get; set; } = new();
    public List<string> Omitidas { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
}
