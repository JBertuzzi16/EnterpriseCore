
using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Repositorios;
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

var app = builder.Build();

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

// Configuración de rutas MVC.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
