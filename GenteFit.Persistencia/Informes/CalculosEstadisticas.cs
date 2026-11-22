using System.Globalization;

namespace GenteFit.Persistencia.Informes;

// Cálculos de los informes separados de la consulta a la base de datos,
// para poder probarlos sin SQL Server.
public static class CalculosEstadisticas
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    // Porcentaje con un decimal. Si el total es 0 devuelve 0 (no hay división por cero).
    public static double Porcentaje(int parte, int total)
    {
        if (total <= 0 || parte <= 0)
            return 0;

        return Math.Round(parte * 100.0 / total, 1, MidpointRounding.AwayFromZero);
    }

    // Ocupación de un conjunto de sesiones: plazas ocupadas entre plazas ofertadas.
    // Se calcula sobre el total (no es la media de los porcentajes), así una sesión
    // vacía pesa lo mismo que una llena.
    public static double OcupacionMedia(IEnumerable<(int Confirmadas, int Aforo)> sesiones)
    {
        int ocupadas = 0, ofertadas = 0;

        foreach (var (confirmadas, aforo) in sesiones)
        {
            ocupadas += Math.Min(confirmadas, aforo);
            ofertadas += aforo;
        }

        return Porcentaje(ocupadas, ofertadas);
    }

    // Devuelve los doce meses del año, con 0 en los meses sin altas.
    public static List<AltasMes> CompletarMeses(IReadOnlyDictionary<int, int> altasPorMes)
    {
        var meses = new List<AltasMes>(12);

        for (int mes = 1; mes <= 12; mes++)
        {
            string nombre = Es.TextInfo.ToTitleCase(Es.DateTimeFormat.GetMonthName(mes));
            meses.Add(new AltasMes(mes, nombre, altasPorMes.TryGetValue(mes, out int total) ? total : 0));
        }

        return meses;
    }

    // Ancho (0-100) de una barra horizontal respecto al valor máximo de la serie.
    public static double AnchoBarra(double valor, double maximo)
    {
        if (maximo <= 0 || valor <= 0)
            return 0;

        return Math.Round(Math.Min(valor, maximo) * 100.0 / maximo, 1);
    }

    // Normaliza un periodo: si llega al revés se intercambia, y el final se toma como día completo.
    public static (DateTime Desde, DateTime HastaExclusivo) NormalizarPeriodo(DateTime desde, DateTime hasta)
    {
        DateTime inicio = desde.Date;
        DateTime fin = hasta.Date;

        if (fin < inicio)
            (inicio, fin) = (fin, inicio);

        return (inicio, fin.AddDays(1));
    }
}
