using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Xml;

namespace GenteFit.Tests;

/// <summary>
/// Pruebas automáticas para comprobar la exportación
/// y posterior lectura XML de las actividades.
/// </summary>
public class ActividadXmlExportadorTests
{
    [Fact]
    public void ExportarYLeer_ConservaTodosLosDatos()
    {
        // Preparamos una actividad de prueba.
        var actividad = new Actividad
        {
            Id = 25,
            Nombre = "Spinning",
            Descripcion = "Clase de ciclismo indoor",
            Intensidad = NivelIntensidad.ALTA,
            DuracionMinutos = 45,
            Activa = false
        };

        // Creamos una ruta temporal para el XML.
        string ruta = Path.Combine(
            Path.GetTempPath(),
            $"gentefit_test_{Guid.NewGuid():N}.xml");

        try
        {
            // Exportamos la actividad.
            ActividadXmlExportador.Exportar(
                new[] { actividad }, ruta);

            // Leemos el fichero generado.
            var lector = new ActividadXmlLector();
            var resultado = lector.Leer(ruta);

            // Comprobamos que no existen errores.
            Assert.False(resultado.HayError);

            // Debe haberse recuperado una actividad.
            var recuperada = Assert.Single(resultado.Elementos);

            // Comparamos todos los campos.
            Assert.Equal(actividad.Id, recuperada.Id);
            Assert.Equal(actividad.Nombre, recuperada.Nombre);
            Assert.Equal(
                actividad.Descripcion,
                recuperada.Descripcion);
            Assert.Equal(
                actividad.Intensidad,
                recuperada.Intensidad);
            Assert.Equal(
                actividad.DuracionMinutos,
                recuperada.DuracionMinutos);
            Assert.Equal(actividad.Activa, recuperada.Activa);
        }
        finally
        {
            // Eliminamos el fichero temporal.
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
    }

    [Fact]
    public void ExportarYLeer_ConservaCaracteresEspeciales()
    {
        var actividad = new Actividad
        {
            Id = 30,
            Nombre = "Natación & relajación",
            Descripcion = "Ejercicios <suaves> para niños y mayores.",
            Intensidad = NivelIntensidad.BAJA,
            DuracionMinutos = 60,
            Activa = true
        };

        string ruta = Path.Combine(
            Path.GetTempPath(),
            $"gentefit_especial_{Guid.NewGuid():N}.xml");

        try
        {
            // Exportamos la actividad.
            ActividadXmlExportador.Exportar(
                new[] { actividad }, ruta);

            // Leemos el XML generado.
            var lector = new ActividadXmlLector();
            var resultado = lector.Leer(ruta);

            Assert.False(resultado.HayError);

            var recuperada = Assert.Single(resultado.Elementos);

            // Los caracteres especiales deben conservarse.
            Assert.Equal(actividad.Id, recuperada.Id);
            Assert.Equal(actividad.Nombre, recuperada.Nombre);
            Assert.Equal(
                actividad.Descripcion,
                recuperada.Descripcion);
        }
        finally
        {
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
    }

    [Fact]
    public void ExportarListaVacia_GeneraXmlValido()
    {
        // Creamos una lista sin actividades.
        var actividades = new List<Actividad>();

        string ruta = Path.Combine(
            Path.GetTempPath(),
            $"gentefit_vacio_{Guid.NewGuid():N}.xml");

        try
        {
            // Exportamos la lista vacía.
            ActividadXmlExportador.Exportar(
                actividades, ruta);

            // Comprobamos que se ha creado el archivo.
            Assert.True(File.Exists(ruta));

            // Comprobamos que el XML puede leerse.
            var lector = new ActividadXmlLector();
            var resultado = lector.Leer(ruta);

            Assert.False(resultado.HayError);
            Assert.Empty(resultado.Elementos);

            // Verificamos que mantiene la estructura XML.
            var documento = System.Xml.Linq.XDocument.Load(ruta);

            Assert.Equal("gentefit",
                documento.Root?.Name.LocalName);

            Assert.NotNull(
                documento.Root?.Element("actividades"));
        }
        finally
        {
            // Eliminamos el fichero temporal.
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void LeerXml_DescartaIdentificadoresInvalidos(
        string identificador)
    {
        // Preparamos un XML con un identificador incorrecto.
        string xml = $"""
        <?xml version="1.0" encoding="utf-8"?>
        <gentefit version="1.0">
          <actividades>
            <actividad id="{identificador}">
              <nombre>Spinning</nombre>
              <descripcion>Clase de ciclismo indoor</descripcion>
              <intensidad>ALTA</intensidad>
              <duracion>45</duracion>
              <activa>true</activa>
            </actividad>
          </actividades>
        </gentefit>
        """;

        // Intentamos leer el XML.
        var lector = new ActividadXmlLector();
        var resultado = lector.LeerTexto(xml);

        // El documento está bien formado.
        Assert.False(resultado.HayError);

        // La actividad no debe aceptarse.
        Assert.Empty(resultado.Elementos);

        // Debe existir un aviso sobre el identificador.
        Assert.Contains(
            resultado.Avisos,
            aviso => aviso.Contains(
                "identificador XML no válido"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Exportar_RutaVacia_LanzaExcepcion(string ruta)
    {
        var actividades = new List<Actividad>
    {
        new Actividad
        {
            Id = 1,
            Nombre = "Yoga",
            Descripcion = "Clase de relajación",
            Intensidad = NivelIntensidad.BAJA,
            DuracionMinutos = 60,
            Activa = true
        }
    };

        // Una ruta vacía o con espacios debe rechazarse.
        Assert.Throws<ArgumentException>(() =>
            ActividadXmlExportador.Exportar(actividades, ruta));
    }

    [Fact]
    public void Exportar_RutaBloqueada_ControlaError()
    {
        string carpeta = Path.Combine(
            Path.GetTempPath(),
            $"gentefit_{Guid.NewGuid():N}");

        Directory.CreateDirectory(carpeta);

        // Creamos un archivo donde debería existir
        // una carpeta.
        string archivoBloqueo = Path.Combine(
            carpeta, "bloqueo");

        File.WriteAllText(archivoBloqueo, "Prueba");

        string rutaXml = Path.Combine(
            archivoBloqueo, "actividades.xml");

        try
        {
            // No se puede crear una carpeta porque
            // ya existe un archivo con ese nombre.
            Assert.ThrowsAny<IOException>(() =>
                ActividadXmlExportador.Exportar(
                    new List<Actividad>(), rutaXml));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void Exportar_IdsDuplicados_RechazaExportacion()
    {
        // Creamos dos actividades con el mismo ID.
        var actividades = new List<Actividad>
    {
        new Actividad
        {
            Id = 10,
            Nombre = "Yoga",
            Descripcion = "Clase de relajación",
            Intensidad = NivelIntensidad.BAJA,
            DuracionMinutos = 60,
            Activa = true
        },
        new Actividad
        {
            Id = 10,
            Nombre = "Spinning",
            Descripcion = "Ciclismo indoor",
            Intensidad = NivelIntensidad.ALTA,
            DuracionMinutos = 45,
            Activa = true
        }
    };

        string ruta = Path.Combine(
            Path.GetTempPath(),
            $"gentefit_duplicados_{Guid.NewGuid():N}.xml");

        try
        {
            // La exportación debe rechazar los IDs repetidos.
            var excepcion = Assert.Throws<ArgumentException>(() =>
                ActividadXmlExportador.Exportar(actividades, ruta));

            // Comprobamos que explica el problema.
            Assert.Contains("duplicado", excepcion.Message);

            // No debe generarse un XML con datos incorrectos.
            Assert.False(File.Exists(ruta));
        }
        finally
        {
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
    }

    [Theory]
    [InlineData("id")]
    [InlineData("nombre")]
    [InlineData("duracion")]
    [InlineData("intensidad")]
    public void Exportar_DatosInvalidos_RechazaExportacion(
        string campo)
    {
        // Creamos una actividad inicialmente válida.
        var actividad = new Actividad
        {
            Id = 10,
            Nombre = "Yoga",
            Descripcion = "Clase de relajación",
            Intensidad = NivelIntensidad.BAJA,
            DuracionMinutos = 60,
            Activa = true
        };

        // Introducimos un error según el caso de prueba.
        switch (campo)
        {
            case "id":
                actividad.Id = 0;
                break;

            case "nombre":
                actividad.Nombre = "   ";
                break;

            case "duracion":
                actividad.DuracionMinutos = -30;
                break;

            case "intensidad":
                actividad.Intensidad = (NivelIntensidad)999;
                break;
        }

        string ruta = Path.Combine(
            Path.GetTempPath(),
            $"gentefit_invalido_{Guid.NewGuid():N}.xml");

        try
        {
            // Los datos inválidos deben provocar una excepción.
            Assert.Throws<ArgumentException>(() =>
                ActividadXmlExportador.Exportar(
                    new[] { actividad }, ruta));

            // No debe generarse un archivo incorrecto.
            Assert.False(File.Exists(ruta));
        }
        finally
        {
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
    }

}