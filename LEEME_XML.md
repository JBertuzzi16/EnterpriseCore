# Lector XML (Producto 2)

Copiad el contenido de este zip en la raíz del repositorio (EnterpriseCore). Se añaden:

- `GenteFit.Persistencia/Xml/`: el lector XML con LINQ to XML, dentro del proyecto de persistencia.
  - `LectorXmlBase.cs`: abre el fichero, controla los errores y recorre los registros. Nunca lanza excepciones hacia la aplicación.
  - `XmlTolerante.cs`: lee cada dato como elemento o atributo, sin distinguir mayúsculas, acentos ni espacios de nombres.
  - `ResultadoLecturaXml.cs`: registros leídos, avisos y error.
  - `ActividadXmlLector.cs` y `ClienteXmlLector.cs`: usan las entidades de `GenteFit.Persistencia.Entidades`.
- `GenteFit.DemoXml/`: consola para el vídeo, con un fichero XML de prueba por cada caso de la rúbrica.
- `GenteFit.slnx`: la misma solución con el proyecto `GenteFit.DemoXml` añadido. Si alguien ha cambiado la solución, en lugar de sobrescribirla añadid el proyecto desde Visual Studio (Agregar > Proyecto existente).

Para ejecutar la demo: establecer `GenteFit.DemoXml` como proyecto de inicio y pulsar F5. Hay una pausa entre casos.

Nota: `ClienteXmlLector` solo convierte el XML en objetos. Para guardar esos clientes en SQL Server
hará falta crearles su Usuario, porque `UsuarioId` es obligatorio.
