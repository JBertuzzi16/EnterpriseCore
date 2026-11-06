using GenteFit.Persistencia.Datos;
using GenteFit.Persistencia.Repositorios;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Todas las páginas exigen sesión iniciada, salvo las marcadas con [AllowAnonymous] (login).
builder.Services.AddControllersWithViews(opciones =>
{
    var politica = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    opciones.Filters.Add(new AuthorizeFilter(politica));
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.LoginPath = "/Cuenta/Login";
        opciones.AccessDeniedPath = "/Cuenta/AccesoDenegado";
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.Name = "GenteFit.Sesion";
    });

builder.Services.AddDbContext<GenteFitDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("GenteFit")
        ?? throw new InvalidOperationException(
            "No se encuentra la conexión GenteFit.")));

builder.Services.AddScoped<SalaRepositorio>();
builder.Services.AddScoped<ActividadRepositorio>();
builder.Services.AddScoped<UsuarioRepositorio>();

var app = builder.Build();

// En desarrollo: crea/actualiza la BD y los usuarios de prueba.
// Si la BD no está disponible, la aplicación arranca igualmente y avisa en el log.
if (app.Environment.IsDevelopment())
{
    using var ambito = app.Services.CreateScope();
    try
    {
        var contexto = ambito.ServiceProvider.GetRequiredService<GenteFitDbContext>();
        contexto.Database.Migrate();
        DatosIniciales.Sembrar(contexto);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "No se pudo preparar la base de datos al arrancar.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
