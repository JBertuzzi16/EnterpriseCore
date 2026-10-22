using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GenteFit.Persistencia.Xml;

/// <summary>
/// Métodos de ayuda para leer XML de forma tolerante a cambios de estructura:
/// - un dato se busca como elemento hijo o como atributo;
/// - no se distinguen mayúsculas, minúsculas ni espacios de nombres;
/// - se admiten nombres alternativos ("nombre", "titulo"…);
/// - si falta un dato o no se puede convertir, se devuelve null en lugar de lanzar una excepción.
/// </summary>
public static class XmlTolerante
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
    private static readonly string[] FormatosFecha =
        { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy HH:mm" };

    /// <summary>Busca en el documento los elementos con cualquiera de los nombres indicados, a cualquier profundidad.</summary>
    public static IEnumerable<XElement> Buscar(XDocument doc, params string[] nombres) =>
        doc.Descendants().Where(x => nombres.Any(n => MismoNombre(x.Name.LocalName, n)));

    /// <summary>Texto de un dato, buscado como elemento hijo o como atributo. Null si no existe o está vacío.</summary>
    public static string? Texto(XElement e, params string[] nombres)
    {
        foreach (var nombre in nombres)
        {
            var hijo = e.Elements().FirstOrDefault(x => MismoNombre(x.Name.LocalName, nombre));
            if (hijo != null) return Limpiar(hijo.Value);

            var atributo = e.Attributes().FirstOrDefault(a => MismoNombre(a.Name.LocalName, nombre));
            if (atributo != null) return Limpiar(atributo.Value);
        }
        return null;
    }

    public static int? Entero(XElement e, params string[] nombres) =>
        int.TryParse(Texto(e, nombres), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : null;

    public static decimal? Decimal(XElement e, params string[] nombres)
    {
        var texto = Texto(e, nombres)?.Replace(',', '.');
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : null;
    }

    public static DateTime? Fecha(XElement e, params string[] nombres)
    {
        var texto = Texto(e, nombres);
        if (texto is null) return null;
        if (DateTime.TryParseExact(texto, FormatosFecha, Es, DateTimeStyles.None, out var exacta)) return exacta;
        return DateTime.TryParse(texto, Es, DateTimeStyles.None, out var libre) ? libre : null;
    }

    public static bool? Booleano(XElement e, params string[] nombres) =>
        QuitarAcentos(Texto(e, nombres)?.ToLowerInvariant()) switch
        {
            "true" or "1" or "si" or "s" or "yes" => true,
            "false" or "0" or "no" or "n" => false,
            _ => null
        };

    /// <summary>Convierte un texto en un valor de enumerado sin distinguir mayúsculas ni acentos ("ALTA", "alta", "Básica"…).</summary>
    public static TEnum? Enumerado<TEnum>(string? texto) where TEnum : struct, Enum
    {
        if (texto is null) return null;
        var buscado = QuitarAcentos(texto.Replace(" ", "")).ToLowerInvariant();
        foreach (var valor in Enum.GetValues<TEnum>())
            if (QuitarAcentos(valor.ToString()).ToLowerInvariant() == buscado) return valor;
        return null;
    }

    private static bool MismoNombre(string a, string b) =>
        string.Equals(QuitarAcentos(a), QuitarAcentos(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Quita espacios sobrantes y saltos de línea; devuelve null si el texto queda vacío.</summary>
    private static string? Limpiar(string? texto)
    {
        if (texto is null) return null;
        var limpio = Regex.Replace(texto, @"\s+", " ").Trim();
        return limpio.Length == 0 ? null : limpio;
    }

    private static string QuitarAcentos(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto ?? "";
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
