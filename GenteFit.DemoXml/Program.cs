using System.Text;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Xml;

// Demostración de la lectura de fuentes XML de GenteFit.
// Cada caso muestra un escenario de la rúbrica: lectura correcta, caracteres especiales,
// cambios de estructura, fichero mal formado, codificación distinta y fichero inexistente.
// Ejecutad con "--sin-pausa" para que no se detenga entre casos.

Console.OutputEncoding = Encoding.UTF8;
bool pausar = !args.Contains("--sin-pausa");
string carpeta = Path.Combine(AppContext.BaseDirectory, "DatosPrueba");

var lectorActividades = new ActividadXmlLector();
var lectorClientes = new ClienteXmlLector();

Caso("1. Lectura correcta", "1_actividades_correcto.xml", lectorActividades, MostrarActividad);
Caso("2. Caracteres especiales (acentos, ñ, apóstrofos, &, CDATA)", "2_actividades_caracteres_especiales.xml", lectorActividades, MostrarActividad);
Caso("3. Cambios de estructura", "3_actividades_estructura_cambiada.xml", lectorActividades, MostrarActividad);
Caso("4. Fichero mal formado", "4_actividades_mal_formado.xml", lectorActividades, MostrarActividad);
Caso("5. Fichero guardado en Latin-1 (ANSI) en lugar de UTF-8", "5_actividades_latin1.xml", lectorActividades, MostrarActividad);
Caso("6. Fuente de datos no disponible", "no_existe.xml", lectorActividades, MostrarActividad);
Caso("7. Lectura de clientes", "6_clientes.xml", lectorClientes, MostrarCliente);

Console.WriteLine("Fin de la demostración.");

void Caso<T>(string titulo, string fichero, LectorXmlBase<T> lector, Func<T, string> mostrar) where T : class
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(new string('=', 70));
    Console.WriteLine(titulo);
    Console.WriteLine($"Fichero: {fichero}");
    Console.WriteLine(new string('=', 70));
    Console.ResetColor();

    var resultado = lector.Leer(Path.Combine(carpeta, fichero));

    Console.WriteLine($"Estado: {resultado.Estado}");
    if (resultado.HayError)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR: {resultado.Error}");
        Console.ResetColor();
    }

    Console.WriteLine($"Registros leídos: {resultado.Elementos.Count}");
    foreach (var elemento in resultado.Elementos)
        Console.WriteLine("  - " + mostrar(elemento));

    if (resultado.Avisos.Count > 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Avisos ({resultado.Avisos.Count}):");
        foreach (var aviso in resultado.Avisos)
            Console.WriteLine("  ! " + aviso);
        Console.ResetColor();
    }

    Console.WriteLine();
    if (pausar)
    {
        Console.WriteLine("Pulsa una tecla para continuar…");
        Console.ReadKey(true);
    }
}

static string MostrarActividad(Actividad a) =>
    $"{a.Nombre} | {a.Intensidad} | {a.DuracionMinutos} min | {a.Descripcion}";

static string MostrarCliente(Cliente c) =>
    $"{c.Dni} | {c.Nombre} {c.Apellidos} | {(c.Email == "" ? "(sin email)" : c.Email)} | " +
    $"nacimiento: {(c.FechaNacimiento == default ? "(pendiente)" : c.FechaNacimiento.ToString("dd/MM/yyyy"))}";
