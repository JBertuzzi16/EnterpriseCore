using System.Reflection;
using GenteFit.Persistencia.Entidades;
using GenteFit.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenteFit.TestsAdmin;

// Comprueba que cada pantalla solo la pueden abrir los roles previstos.
// Se lee el atributo [Authorize(Roles = ...)] de cada controlador, que es lo que aplica ASP.NET Core.
public class PermisosRolesTests
{
    public static TheoryData<Type, string[]> RolesPorControlador => new()
    {
        { typeof(AdminController),         new[] { "ADMIN" } },
        { typeof(InformesController),      new[] { "ADMIN" } },
        { typeof(ConfiguracionController), new[] { "ADMIN" } },
        { typeof(SalasController),         new[] { "ENCARGADO", "ADMIN" } },
        { typeof(ActividadesController),   new[] { "ENCARGADO", "ADMIN" } },
        { typeof(ClienteController),       new[] { "CLIENTE" } }
    };

    [Theory]
    [MemberData(nameof(RolesPorControlador))]
    public void Controlador_SoloPermiteSusRoles(Type controlador, string[] esperados)
    {
        string[] permitidos = RolesPermitidos(controlador);

        Assert.Equal(esperados.OrderBy(r => r), permitidos.OrderBy(r => r));
    }

    [Theory]
    [InlineData(Rol.CLIENTE)]
    [InlineData(Rol.RECEPCIONISTA)]
    [InlineData(Rol.ENCARGADO)]
    [InlineData(Rol.MONITOR)]
    public void Administracion_NoEsAccesibleParaOtrosRoles(Rol rol)
    {
        foreach (Type controlador in new[] { typeof(AdminController), typeof(InformesController), typeof(ConfiguracionController) })
            Assert.DoesNotContain(rol.ToString(), RolesPermitidos(controlador));
    }

    [Fact]
    public void Administrador_NoEntraEnElPortalDelCliente()
    {
        Assert.DoesNotContain(nameof(Rol.ADMIN), RolesPermitidos(typeof(ClienteController)));
    }

    [Fact]
    public void Administracion_NingunaAccionPermiteAnonimos()
    {
        foreach (Type controlador in new[] { typeof(AdminController), typeof(InformesController), typeof(ConfiguracionController) })
        {
            Assert.Null(controlador.GetCustomAttribute<AllowAnonymousAttribute>());

            var anonimas = controlador
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);

            Assert.Empty(anonimas);
        }
    }

    [Fact]
    public void Administracion_LasAccionesQueModificanExigenPostYTokenAntifalsificacion()
    {
        string[] acciones = { "CrearUsuario", "EditarUsuario", "Desactivar", "Activar", "RestablecerContrasena", "ImportarXml" };

        var metodos = new[] { typeof(AdminController), typeof(ConfiguracionController) }
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => acciones.Contains(m.Name) && m.GetCustomAttribute<HttpPostAttribute>() != null)
            .ToList();

        Assert.NotEmpty(metodos);
        Assert.All(metodos, m => Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }

    // Roles del atributo [Authorize] de la clase (cadena "A,B" -> ["A","B"]).
    private static string[] RolesPermitidos(Type controlador) =>
        controlador.GetCustomAttributes<AuthorizeAttribute>()
            .SelectMany(a => (a.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
}
