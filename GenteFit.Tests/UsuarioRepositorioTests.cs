
using System.Text.Json;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Persistencia.Seguridad;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenteFit.Tests;

public class UsuarioRepositorioTests : IDisposable
{
    private readonly GenteFitDbContext _contexto;
    private readonly UsuarioRepositorio _repositorio;
    private readonly DbContextOptions<GenteFitDbContext> _opciones;

    public UsuarioRepositorioTests()
    {
        // Leemos la conexión del proyecto web.
        string ruta = BuscarConfiguracion();

        using var documento = JsonDocument.Parse(
            File.ReadAllText(ruta),
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        string cadenaOriginal = documento.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("GenteFit")
            .GetString()
            ?? throw new Exception(
                "No se encontró la conexión GenteFit.");

        // Creamos una base diferente para cada prueba.
        // Nunca utilizamos directamente la BD principal.
        var conexion = new SqlConnectionStringBuilder(
            cadenaOriginal);

        conexion.InitialCatalog =
            "GenteFit_Test_" + Guid.NewGuid().ToString("N");

        _opciones =
            new DbContextOptionsBuilder<GenteFitDbContext>()
                .UseSqlServer(conexion.ConnectionString)
                .Options;

        _contexto = new GenteFitDbContext(_opciones);

        // Crear las tablas de la BD temporal.
        _contexto.Database.EnsureCreated();

        _repositorio = new UsuarioRepositorio(_contexto);
    }

    // Preparamos un usuario para las pruebas.
    private void CrearUsuario(bool activo = true)
    {
        var usuario = new Usuario
        {
            NombreUsuario = "cliente_test",
            Email = "cliente.test@gentefit.test",
            HashContrasena =
                HashContrasena.Crear("Gente.Fit1"),
            Rol = Rol.CLIENTE,
            Activo = activo
        };

        _contexto.Usuarios.Add(usuario);
        _contexto.SaveChanges();
    }

    // PRUEBA 1: credenciales correctas.
    [Fact]
    public void Login_CredencialesCorrectas_PermiteAcceso()
    {
        CrearUsuario();

        var resultado = _repositorio.ValidarCredenciales(
            "cliente.test@gentefit.test",
            "Gente.Fit1");

        Assert.NotNull(resultado);
        Assert.Equal(Rol.CLIENTE, resultado.Rol);
    }

    // PRUEBA 2: contraseña incorrecta.
    [Fact]
    public void Login_ContrasenaIncorrecta_RechazaAcceso()
    {
        CrearUsuario();

        var resultado = _repositorio.ValidarCredenciales(
            "cliente.test@gentefit.test",
            "ContrasenaIncorrecta");

        Assert.Null(resultado);
    }

    // PRUEBA 3: cuenta inactiva.
    [Fact]
    public void Login_UsuarioInactivo_RechazaAcceso()
    {
        CrearUsuario(activo: false);

        var resultado = _repositorio.ValidarCredenciales(
            "cliente.test@gentefit.test",
            "Gente.Fit1");

        Assert.Null(resultado);
    }

    // PRUEBA 4: usuario inexistente.
    [Fact]
    public void Login_UsuarioInexistente_RechazaAcceso()
    {
        var resultado = _repositorio.ValidarCredenciales(
            "noexiste@gentefit.test",
            "Gente.Fit1");

        Assert.Null(resultado);
    }

    // PRUEBA 5: seguridad de contraseñas.
    [Fact]
    public void Contrasena_HashSeguro_VerificaCorrectamente()
    {
        string contrasena = "Gente.Fit1";

        string hash1 = HashContrasena.Crear(contrasena);
        string hash2 = HashContrasena.Crear(contrasena);

        // La contraseña no se almacena en claro.
        Assert.NotEqual(contrasena, hash1);

        // Comprobamos el formato PBKDF2.
        Assert.StartsWith("PBKDF2$", hash1);

        // La contraseña correcta debe verificarse.
        Assert.True(
            HashContrasena.Verificar(contrasena, hash1));

        // Una contraseña incorrecta debe rechazarse.
        Assert.False(
            HashContrasena.Verificar("Incorrecta", hash1));

        // La sal aleatoria genera hashes diferentes.
        Assert.NotEqual(hash1, hash2);
    }

    // Cada prueba elimina su propia base de datos.
    public void Dispose()
    {
        _contexto.Dispose();

        using var limpieza =
            new GenteFitDbContext(_opciones);

        limpieza.Database.EnsureDeleted();

        GC.SuppressFinalize(this);
    }

    // Localizamos appsettings.json en la solución.
    private static string BuscarConfiguracion()
    {
        DirectoryInfo? carpeta =
            new DirectoryInfo(AppContext.BaseDirectory);

        while (carpeta != null)
        {
            string candidato = Path.Combine(
                carpeta.FullName,
                "GenteFit.web",
                "appsettings.json");

            if (File.Exists(candidato))
                return candidato;

            carpeta = carpeta.Parent;
        }

        throw new FileNotFoundException(
            "No se encontró GenteFit.web/appsettings.json.");
    }
}
