using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GenteFit.Persistencia.Xml;

/// <summary>
/// Lector genérico de fuentes XML con LINQ to XML (XDocument).
/// Se encarga de todo lo que es común a cualquier tipo de dato: abrir el fichero, controlar los errores
/// y recorrer los registros. Cada clase hija solo indica cómo se llama el elemento y cómo convertirlo en objeto.
/// </summary>
public abstract class LectorXmlBase<T> where T : class
{
    /// <summary>Nombres que puede tener el elemento de cada registro (por ejemplo "actividad" o "clase").</summary>
    protected abstract string[] NombresElemento { get; }

    /// <summary>Convierte un elemento en objeto. Devuelve null para descartar el registro (añadiendo un aviso).</summary>
    protected abstract T? Mapear(XElement elemento, string ubicacion, List<string> avisos);

    /// <summary>Lee un fichero XML. Nunca lanza excepciones: los errores se devuelven en el resultado.</summary>
    public ResultadoLecturaXml<T> Leer(string ruta)
    {
        var resultado = new ResultadoLecturaXml<T>();

        if (string.IsNullOrWhiteSpace(ruta))
        {
            resultado.MarcarError("No se ha indicado la ruta del fichero XML.");
            return resultado;
        }
        if (!File.Exists(ruta))
        {
            resultado.MarcarError($"No se encuentra el fichero XML: {Path.GetFullPath(ruta)}");
            return resultado;
        }

        try
        {
            var documento = Cargar(ruta, resultado.Avisos);
            Procesar(documento, resultado);
        }
        catch (XmlException ex)
        {
            resultado.MarcarError($"El fichero XML no está bien formado (línea {ex.LineNumber}, posición {ex.LinePosition}): {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            resultado.MarcarError($"No hay permisos para leer el fichero: {ruta}");
        }
        catch (IOException ex)
        {
            resultado.MarcarError($"No se ha podido abrir el fichero; puede que otro programa lo esté usando ({ex.Message}).");
        }
        return resultado;
    }

    /// <summary>Lee XML a partir de un texto (útil para pruebas o para datos recibidos de otro sistema).</summary>
    public ResultadoLecturaXml<T> LeerTexto(string xml)
    {
        var resultado = new ResultadoLecturaXml<T>();
        try
        {
            Procesar(XDocument.Parse(xml, LoadOptions.SetLineInfo), resultado);
        }
        catch (XmlException ex)
        {
            resultado.MarcarError($"El texto XML no está bien formado (línea {ex.LineNumber}, posición {ex.LinePosition}): {ex.Message}");
        }
        return resultado;
    }

    /// <summary>
    /// Carga el documento respetando la codificación que declare. Si falla, prueba una vez como Latin-1 (ISO-8859-1),
    /// que es como suelen guardar los editores de Windows en "ANSI": así los acentos y la ñ no rompen la lectura.
    /// </summary>
    private static XDocument Cargar(string ruta, List<string> avisos)
    {
        try
        {
            return XDocument.Load(ruta, LoadOptions.SetLineInfo);
        }
        catch (XmlException original)
        {
            try
            {
                using var lector = new StreamReader(ruta, Encoding.Latin1);
                var documento = XDocument.Load(lector, LoadOptions.SetLineInfo);
                avisos.Add("El fichero no estaba guardado en UTF-8: se ha leído como ISO-8859-1 (Latin-1).");
                return documento;
            }
            catch (XmlException)
            {
                throw original;   // el problema no era la codificación: se informa del error original
            }
        }
    }

    private void Procesar(XDocument documento, ResultadoLecturaXml<T> resultado)
    {
        var nodos = XmlTolerante.Buscar(documento, NombresElemento).ToList();
        if (nodos.Count == 0)
        {
            resultado.Avisos.Add($"No se ha encontrado ningún elemento <{NombresElemento[0]}> en el documento.");
            return;
        }

        for (int i = 0; i < nodos.Count; i++)
        {
            var ubicacion = Ubicacion(nodos[i], i + 1);
            try
            {
                var objeto = Mapear(nodos[i], ubicacion, resultado.Avisos);
                if (objeto != null) resultado.Elementos.Add(objeto);
            }
            catch (Exception ex)
            {
                // Un registro problemático no debe impedir leer los demás.
                resultado.Avisos.Add($"{ubicacion}: registro descartado por un error inesperado ({ex.Message}).");
            }
        }
    }

    private static string Ubicacion(XElement elemento, int posicion) =>
        elemento is IXmlLineInfo info && info.HasLineInfo()
            ? $"Registro {posicion} (línea {info.LineNumber})"
            : $"Registro {posicion}";
}
