using GenteFit.Persistencia.Repositorios;
using GenteFit.Persistencia.Xml;

namespace GenteFit.Persistencia.Servicios;

/// <summary>
/// Servicio que conecta el repositorio de actividades
/// con la funcionalidad de exportación XML.
/// </summary>
public class ActividadExportacionXmlServicio
{
    private readonly ActividadRepositorio _repositorio;

    public ActividadExportacionXmlServicio(
        ActividadRepositorio repositorio)
    {
        _repositorio = repositorio
            ?? throw new ArgumentNullException(nameof(repositorio));
    }

    /// <summary>
    /// Obtiene las actividades de la base de datos
    /// y las exporta a un archivo XML.
    /// Devuelve el número de actividades exportadas.
    /// </summary>
    public int Exportar(string ruta)
    {
        var actividades = _repositorio.ObtenerTodas();

        ActividadXmlExportador.Exportar(actividades, ruta);

        return actividades.Count;
    }
}
