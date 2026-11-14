using System.Text.RegularExpressions;
using System.Xml.Linq;
using GenteFit.Persistencia.Entidades;

namespace GenteFit.Persistencia.Xml;

/// <summary>
/// Lee clientes desde una fuente XML.
/// Solo convierte el XML en objetos Cliente: para guardarlos en SQL Server hará falta
/// asignarles después su Usuario (UsuarioId es obligatorio en la base de datos).
/// </summary>
public class ClienteXmlLector : LectorXmlBase<Cliente>
{
    private static readonly Regex FormatoDni = new(@"^[0-9XYZ][0-9]{7}[A-Z]$", RegexOptions.IgnoreCase);

    protected override string[] NombresElemento => new[] { "cliente", "socio" };

    protected override Cliente? Mapear(XElement e, string ubicacion, List<string> avisos)
    {
        var dni = XmlTolerante.Texto(e, "dni", "nif", "documento")?.Replace("-", "").Replace(" ", "").ToUpperInvariant();
        var nombre = XmlTolerante.Texto(e, "nombre");

        if (dni is null || nombre is null)
        {
            avisos.Add($"{ubicacion}: falta el DNI o el nombre, el cliente se descarta.");
            return null;
        }
        if (!FormatoDni.IsMatch(dni))
            avisos.Add($"{ubicacion} ({nombre}): el DNI «{dni}» no tiene un formato válido; se importa igualmente para revisarlo.");

        var cliente = new Cliente
        {
            Dni = dni,
            Nombre = nombre,
            Apellidos = XmlTolerante.Texto(e, "apellidos", "apellido") ?? "",
            Email = XmlTolerante.Texto(e, "email", "correo", "mail") ?? "",
            Telefono = XmlTolerante.Texto(e, "telefono", "movil") ?? "",
            Activo = XmlTolerante.Booleano(e, "activo", "activa") ?? true,
            FechaAlta = XmlTolerante.Fecha(e, "fechaAlta", "alta") ?? DateTime.Today
        };

        if (cliente.Email == "")
            avisos.Add($"{ubicacion} ({nombre}): no tiene email, que es obligatorio para guardarlo en la base de datos.");

        var textoNacimiento = XmlTolerante.Texto(e, "fechaNacimiento", "nacimiento");
        var nacimiento = XmlTolerante.Fecha(e, "fechaNacimiento", "nacimiento");
        if (nacimiento is null)
            avisos.Add($"{ubicacion} ({nombre}): fecha de nacimiento " +
                       (textoNacimiento is null ? "ausente" : $"«{textoNacimiento}» no válida") +
                       ", queda pendiente de revisar.");
        else cliente.FechaNacimiento = nacimiento.Value;

        return cliente;
    }
}
