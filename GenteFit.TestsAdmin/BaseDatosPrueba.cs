using System.Text.Json;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Seguridad;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.TestsAdmin;

// Base de datos temporal para cada prueba, igual que en GenteFit.Tests:
// se lee la conexión de GenteFit.web/appsettings.json, se crea una BD con nombre único
// y se borra al terminar. Nunca se toca la base de datos principal.
public abstract class BaseDatosPrueba : IDisposable
{
    protected readonly GenteFitDbContext Contexto;
    private readonly DbContextOptions<GenteFitDbContext> _opciones;

    // crearTablas = false deja la base de datos sin crear, para probar las migraciones.
    protected BaseDatosPrueba(bool crearTablas = true)
    {
        var conexion = new SqlConnectionStringBuilder(LeerCadenaConexion())
        {
            InitialCatalog = "GenteFit_TestAdmin_" + Guid.NewGuid().ToString("N")
        };

        _opciones = new DbContextOptionsBuilder<GenteFitDbContext>()
            .UseSqlServer(conexion.ConnectionString)
            .Options;

        Contexto = new GenteFitDbContext(_opciones);

        if (crearTablas)
            Contexto.Database.EnsureCreated();
    }

    // Contexto nuevo (sin entidades en memoria) para comprobar lo que de verdad se ha guardado.
    protected GenteFitDbContext ContextoNuevo() => new(_opciones);

    protected Usuario CrearUsuario(string nombre, Rol rol, bool activo = true)
    {
        var usuario = new Usuario
        {
            NombreUsuario = nombre,
            Email = $"{nombre}@gentefit.test",
            HashContrasena = HashContrasena.Crear("Gente.Fit1"),
            Rol = rol,
            Activo = activo
        };

        Contexto.Usuarios.Add(usuario);
        Contexto.SaveChanges();
        return usuario;
    }

    public void Dispose()
    {
        Contexto.Dispose();

        using var limpieza = new GenteFitDbContext(_opciones);
        limpieza.Database.EnsureDeleted();

        GC.SuppressFinalize(this);
    }

    private static string LeerCadenaConexion()
    {
        DirectoryInfo? carpeta = new(AppContext.BaseDirectory);

        while (carpeta != null)
        {
            string candidato = Path.Combine(carpeta.FullName, "GenteFit.web", "appsettings.json");

            if (File.Exists(candidato))
            {
                using var documento = JsonDocument.Parse(File.ReadAllText(candidato), new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                return documento.RootElement.GetProperty("ConnectionStrings").GetProperty("GenteFit").GetString()
                       ?? throw new InvalidOperationException("No se encontró la conexión GenteFit.");
            }

            carpeta = carpeta.Parent;
        }

        throw new FileNotFoundException("No se encontró GenteFit.web/appsettings.json.");
    }
}
