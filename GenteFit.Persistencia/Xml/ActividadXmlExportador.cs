using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GenteFit.Persistencia.Entidades;

namespace GenteFit.Persistencia.Xml;

/// <summary>
/// Exporta actividades de GenteFit a un archivo XML
/// para intercambiar información con Odoo.
/// </summary>
public static class ActividadXmlExportador
{
    public static void Exportar(
        IEnumerable<Actividad> actividades,
        string ruta)
    {
        ArgumentNullException.ThrowIfNull(actividades);

        if (string.IsNullOrWhiteSpace(ruta))
        {
            throw new ArgumentException(
                "Debe indicarse una ruta de exportación.",
                nameof(ruta));
        }

        // Materializamos las actividades para validarlas
        // antes de generar el documento XML.
        var lista = actividades.ToList();

        foreach (var actividad in lista)
        {
            if (actividad is null)
                throw new ArgumentException(
                    "La lista contiene una actividad nula.");

            if (actividad.Id <= 0)
                throw new ArgumentException(
                    "El identificador de la actividad debe ser positivo.");

            if (string.IsNullOrWhiteSpace(actividad.Nombre))
                throw new ArgumentException(
                    $"La actividad {actividad.Id} no tiene nombre.");

            if (actividad.DuracionMinutos <= 0)
                throw new ArgumentException(
                    $"La actividad {actividad.Id} tiene una duración no válida.");

            if (!Enum.IsDefined(typeof(NivelIntensidad), actividad.Intensidad))
                throw new ArgumentException(
                    $"La actividad {actividad.Id} tiene una intensidad no válida.");
        }

        // Comprobamos que no haya identificadores repetidos.
        var duplicado = lista
            .GroupBy(a => a.Id)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicado != null)
        {
            throw new ArgumentException(
                $"El identificador {duplicado.Key} está duplicado.");
        }

        // Construimos el documento XML.
        var documento = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("gentefit",
                new XAttribute("version", "1.0"),
                new XElement("actividades",
                    lista.Select(a =>
                        new XElement("actividad",
                            new XAttribute("id", a.Id),
                            new XElement("nombre", a.Nombre),
                            new XElement("descripcion", a.Descripcion),
                            new XElement("intensidad", a.Intensidad.ToString()),
                            new XElement("duracion", a.DuracionMinutos),
                            new XElement("activa", a.Activa)
                        )
                    )
                )
            )
        );

        // Preparamos la carpeta de destino.
        string rutaCompleta = Path.GetFullPath(ruta);
        string? carpeta = Path.GetDirectoryName(rutaCompleta);

        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        // Guardamos el documento XML.
        documento.Save(rutaCompleta);
    }
}
