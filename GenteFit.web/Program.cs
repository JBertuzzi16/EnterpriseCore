
using System.Diagnostics;
using System.Net.Sockets;
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Repositorios;
using GenteFit.Web.Servicios;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Todas las páginas requieren autenticación,
// excepto las marcadas con [AllowAnonymous].
builder.Services.AddControllersWithViews(opciones =>
{
    var politica = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    opciones.Filters.Add(new AuthorizeFilter(politica));
});

// Autenticación mediante cookies.
builder.Services.AddAuthentication(
    CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.LoginPath = "/Cuenta/Login";
        opciones.AccessDeniedPath = "/Cuenta/AccesoDenegado";
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.Name = "GenteFit.Sesion";
    });

// Conexión a SQL Server.
builder.Services.AddDbContext<GenteFitDbContext>(opciones =>
    opciones.UseSqlServer(
        builder.Configuration.GetConnectionString("GenteFit")
        ?? throw new InvalidOperationException(
            "No se encuentra la conexión GenteFit.")));

// Repositorios.
builder.Services.AddScoped<SalaRepositorio>();
builder.Services.AddScoped<ActividadRepositorio>();
builder.Services.AddScoped<UsuarioRepositorio>();
builder.Services.AddScoped<ReservaRepositorio>();
builder.Services.AddScoped<SesionRepositorio>();
builder.Services.AddScoped<ClienteRepositorio>();

// Administración: usuarios, estadísticas y configuración del centro.
builder.Services.AddScoped<AdminUsuarioRepositorio>();
builder.Services.AddScoped<EstadisticasRepositorio>();
builder.Services.AddSingleton(sp => new ConfiguracionCentroServicio(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<ILogger<ConfiguracionCentroServicio>>()));

var app = builder.Build();

// ---------------------------------------------------------------------------
// Modo instalación: GenteFit.web.exe --preparar-bd [--admin-email X --admin-contrasena Y] [--demo]
// Lo ejecuta el instalador para crear la base de datos y el primer administrador.
// No arranca el servidor web: prepara la BD, informa y termina.
// ---------------------------------------------------------------------------
if (args.Contains("--preparar-bd"))
{
    string? Argumento(string nombre)
    {
        int posicion = Array.IndexOf(args, nombre);
        return posicion >= 0 && posicion + 1 < args.Length ? args[posicion + 1] : null;
    }

    using var ambitoInstalacion = app.Services.CreateScope();
    try
    {
        var contexto = ambitoInstalacion.ServiceProvider.GetRequiredService<GenteFitDbContext>();
        foreach (string paso in PreparacionBaseDatos.Preparar(
                     contexto,
                     Argumento("--admin-email"),
                     Argumento("--admin-contrasena"),
                     args.Contains("--demo")))
        {
            RegistrarInstalacion(paso);
        }

        RegistrarInstalacion("Base de datos preparada correctamente.");
        Environment.ExitCode = 0;
    }
    catch (Exception ex)
    {
        RegistrarInstalacion("ERROR: no se ha podido preparar la base de datos: " + ex.Message);
        if (ex.InnerException != null)
            RegistrarInstalacion("Detalle: " + ex.InnerException.Message);
        Environment.ExitCode = 1;
    }

    if (!Console.IsInputRedirected && Environment.UserInteractive && !args.Contains("--sin-pausa"))
    {
        // Lanzado desde el menú Inicio ("Reparar la base de datos"): se deja leer el resultado.
        Console.WriteLine();
        Console.WriteLine("Pulsa una tecla para cerrar.");
        try { Console.ReadKey(true); } catch (InvalidOperationException) { }
    }

    return;

    // Muestra cada paso y lo guarda en preparar-bd.log (si la carpeta permite escribir).
    void RegistrarInstalacion(string texto)
    {
        Console.WriteLine(texto);
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "preparar-bd.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {texto}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sin permiso de escritura (por ejemplo, Archivos de programa sin elevar): solo consola.
        }
    }
}

// Preparación de la BD y usuarios de prueba.
// Si SQL Server falla, se registra el problema
// y la aplicación puede seguir arrancando.
if (app.Environment.IsDevelopment())
{
    using var ambito = app.Services.CreateScope();

    try
    {
        var contexto = ambito.ServiceProvider
            .GetRequiredService<GenteFitDbContext>();

        contexto.Database.Migrate();
        DatosIniciales.Sembrar(contexto);
        DatosDemoCatalogo.Sembrar(contexto);
        DatosDemoInformes.Sembrar(contexto);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex,
            "No se pudo preparar la base de datos al arrancar.");
    }
}

// Tratamiento global de errores inesperados.
// Se utiliza tanto en desarrollo como en producción.
app.UseExceptionHandler("/Home/Error");

// HSTS únicamente en producción.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Ruta temporal para comprobar los errores.
// Solo existe cuando ejecutamos en desarrollo.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/Pruebas/Error", () =>
    {
        throw new InvalidOperationException(
            "Error de prueba del sistema GenteFit.");
    })
    .RequireAuthorization();
}
//TEMPORAL

/* if (app.Environment.IsDevelopment())
{
    app.MapGet("/Pruebas/Sesiones",
        async (SesionRepositorio repositorio) =>
        {
            return await repositorio.ObtenerProximasAsync();
        })
        .RequireAuthorization();
} */
// Configuración de rutas MVC.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// ---------------------------------------------------------------------------
// Arranque desde el acceso directo del escritorio (--abrir-navegador):
// si GenteFit ya está en marcha, solo se abre el navegador; si no, se arranca
// el servidor y se abre el navegador cuando está listo.
// ---------------------------------------------------------------------------
if (args.Contains("--abrir-navegador"))
{
    string direccion = (app.Configuration["Urls"] ?? "http://localhost:5190")
        .Split(';', StringSplitOptions.RemoveEmptyEntries)[0]
        .Replace("://*", "://localhost").Replace("://+", "://localhost");

    void AbrirNavegador()
    {
        try
        {
            Process.Start(new ProcessStartInfo(direccion) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "No se pudo abrir el navegador. Abre {Direccion} a mano.", direccion);
        }
    }

    var uri = new Uri(direccion);
    bool yaEnMarcha;
    try
    {
        using var cliente = new TcpClient();
        yaEnMarcha = cliente.ConnectAsync(uri.Host, uri.Port).Wait(TimeSpan.FromMilliseconds(500));
    }
    catch (Exception)
    {
        yaEnMarcha = false;
    }

    if (yaEnMarcha)
    {
        AbrirNavegador();
        return;
    }

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Console.WriteLine();
        Console.WriteLine($"GenteFit está en marcha en {direccion}");
        Console.WriteLine("No cierres esta ventana mientras uses la aplicación.");
        AbrirNavegador();
    });
}

app.Run();
