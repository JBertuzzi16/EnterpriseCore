namespace GenteFit.Persistencia.Xml;

public enum EstadoLectura { Correcta, CorrectaConAvisos, Error }

/// <summary>
/// Resultado de leer una fuente XML. Nunca se lanza una excepción hacia la aplicación:
/// los problemas graves quedan en <see cref="Error"/> y los leves en <see cref="Avisos"/>.
/// </summary>
public class ResultadoLecturaXml<T>
{
    /// <summary>Registros leídos correctamente (o corregidos con valores por defecto).</summary>
    public List<T> Elementos { get; } = new();

    /// <summary>Incidencias que no impiden la lectura: valores por defecto, registros descartados, codificación…</summary>
    public List<string> Avisos { get; } = new();

    /// <summary>Error que impide leer el fichero (no existe, está mal formado, sin permisos…). Null si no lo hay.</summary>
    public string? Error { get; private set; }

    public bool HayError => Error != null;

    public EstadoLectura Estado =>
        Error != null ? EstadoLectura.Error :
        Avisos.Count > 0 ? EstadoLectura.CorrectaConAvisos :
        EstadoLectura.Correcta;

    internal void MarcarError(string mensaje) => Error = mensaje;
}
