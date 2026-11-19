namespace GenteFit.Persistencia.Informes;

// Resultados de los informes del administrador. Son solo datos (sin lógica de base de datos),
// así se pueden mostrar en pantalla, exportar a CSV o comprobar en las pruebas.

public record ResumenEstadisticas(
    int ClientesActivos,
    int AltasEnPeriodo,
    int SesionesProgramadas,
    int ReservasConfirmadas,
    int ReservasEnEspera,
    int ReservasAnuladas,
    double OcupacionMediaPorcentaje,
    double TasaAnulacionPorcentaje);

public record OcupacionSesion(
    int SesionId,
    DateTime Inicio,
    string Actividad,
    string Sala,
    int Aforo,
    int Confirmadas,
    int EnEspera,
    int Anuladas,
    double OcupacionPorcentaje)
{
    public bool Completa => Confirmadas >= Aforo;
    public int PlazasLibres => Math.Max(0, Aforo - Confirmadas);
}

public record RankingActividad(
    string Actividad,
    int Sesiones,
    int Confirmadas,
    int EnEspera,
    double OcupacionMediaPorcentaje);

public record OcupacionSala(
    string Sala,
    int Sesiones,
    int PlazasOfertadas,
    int Confirmadas,
    double OcupacionPorcentaje);

public record AltasMes(int Mes, string NombreMes, int Altas);
