using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GenteFit.Web.Servicios;

// Datos del centro que el administrador puede cambiar sin tocar el código.
// Los campos opcionales son "string?" para que ASP.NET Core no los marque como obligatorios.
public class ConfiguracionCentro
{
    [Required(ErrorMessage = "El nombre del centro es obligatorio.")]
    [StringLength(80)]
    [Display(Name = "Nombre del centro")]
    public string NombreCentro { get; set; } = "GenteFit";

    [StringLength(150)]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; } = "";

    [StringLength(30)]
    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; } = "";

    [EmailAddress(ErrorMessage = "El email de contacto no tiene un formato válido.")]
    [StringLength(254)]
    [Display(Name = "Email de contacto")]
    public string? EmailContacto { get; set; } = "";

    [Display(Name = "Hora de apertura")]
    public TimeOnly HoraApertura { get; set; } = new(7, 0);

    [Display(Name = "Hora de cierre")]
    public TimeOnly HoraCierre { get; set; } = new(22, 0);

    [StringLength(260)]
    [Display(Name = "Ruta de la fuente XML de actividades")]
    public string? RutaFuenteXml { get; set; } = "";
}

// Guarda la configuración en un fichero JSON en una carpeta con permiso de escritura
// (por defecto C:\ProgramData\GenteFit), porque la carpeta de instalación
// (Archivos de programa) es de solo lectura para un usuario normal.
// Si el fichero falta o está dañado, se usan los valores por defecto y la aplicación sigue funcionando.
public class ConfiguracionCentroServicio
{
    public const string NombreFichero = "configuracion-centro.json";

    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    private readonly object _bloqueo = new();
    private readonly ILogger<ConfiguracionCentroServicio> _log;
    private ConfiguracionCentro? _cache;

    public string RutaFichero { get; }

    public ConfiguracionCentroServicio(IConfiguration configuracion, ILogger<ConfiguracionCentroServicio> log)
        : this(CarpetaPorDefecto(configuracion), log)
    {
    }

    // Constructor para las pruebas: permite usar una carpeta temporal.
    public ConfiguracionCentroServicio(string carpeta, ILogger<ConfiguracionCentroServicio> log)
    {
        _log = log;
        RutaFichero = Path.Combine(carpeta, NombreFichero);
    }

    public ConfiguracionCentro Obtener()
    {
        lock (_bloqueo)
        {
            return Copiar(_cache ??= Leer());
        }
    }

    public void Guardar(ConfiguracionCentro configuracion)
    {
        if (configuracion.HoraCierre <= configuracion.HoraApertura)
            throw new ArgumentException("La hora de cierre debe ser posterior a la de apertura.");

        lock (_bloqueo)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaFichero)!);

            // Se escribe en un fichero temporal y luego se sustituye, para no dejarlo a medias.
            string temporal = RutaFichero + ".tmp";
            File.WriteAllText(temporal, JsonSerializer.Serialize(configuracion, OpcionesJson));
            File.Move(temporal, RutaFichero, overwrite: true);

            _cache = Copiar(configuracion);
        }
    }

    private ConfiguracionCentro Leer()
    {
        try
        {
            if (!File.Exists(RutaFichero))
                return new ConfiguracionCentro();

            return JsonSerializer.Deserialize<ConfiguracionCentro>(File.ReadAllText(RutaFichero))
                   ?? new ConfiguracionCentro();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.LogWarning(ex, "No se pudo leer {Ruta}; se usan los valores por defecto.", RutaFichero);
            return new ConfiguracionCentro();
        }
    }

    private static ConfiguracionCentro Copiar(ConfiguracionCentro origen) =>
        JsonSerializer.Deserialize<ConfiguracionCentro>(JsonSerializer.Serialize(origen))!;

    private static string CarpetaPorDefecto(IConfiguration configuracion) =>
        configuracion["GenteFit:CarpetaDatos"] is { Length: > 0 } carpeta
            ? Environment.ExpandEnvironmentVariables(carpeta)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GenteFit");
}
