using System.Xml.Linq;
using GenteFit.Persistencia.Entidades;

namespace GenteFit.Persistencia.Xml;

/// <summary>Lee actividades desde una fuente XML.</summary>
public class ActividadXmlLector : LectorXmlBase<Actividad>
{
    private const int DuracionPorDefecto = 60;

    protected override string[] NombresElemento => new[] { "actividad", "clase" };

    protected override Actividad? Mapear(XElement e, string ubicacion, List<string> avisos)
    {
        var nombre = XmlTolerante.Texto(e, "nombre", "titulo");
        if (nombre is null)
        {
            avisos.Add($"{ubicacion}: la actividad no tiene nombre y se descarta.");
            return null;
        }

        var actividad = new Actividad
        {
            Nombre = nombre,
            Descripcion = XmlTolerante.Texto(e, "descripcion", "detalle") ?? "",
            Activa = XmlTolerante.Booleano(e, "activa", "activo") ?? true
        };

        var textoIntensidad = XmlTolerante.Texto(e, "intensidad", "nivel", "nivelIntensidad");
        var intensidad = XmlTolerante.Enumerado<NivelIntensidad>(textoIntensidad);
        if (intensidad is null)
        {
            avisos.Add($"{ubicacion} ({nombre}): intensidad {(textoIntensidad is null ? "ausente" : $"«{textoIntensidad}» no válida")}, se asigna MEDIA.");
            actividad.Intensidad = NivelIntensidad.MEDIA;
        }
        else actividad.Intensidad = intensidad.Value;

        var duracion = XmlTolerante.Entero(e, "duracion", "duracionMinutos", "minutos");
        if (duracion is null or <= 0)
        {
            avisos.Add($"{ubicacion} ({nombre}): duración ausente o no válida, se asignan {DuracionPorDefecto} minutos.");
            actividad.DuracionMinutos = DuracionPorDefecto;
        }
        else actividad.DuracionMinutos = duracion.Value;

        return actividad;
    }
}
