using GenteFit.Persistencia.Repositorios;

namespace GenteFit.TestsAdmin;

// Validaciones de los datos de una cuenta (no necesitan base de datos).
public class ValidacionesUsuarioTests
{
    [Theory]
    [InlineData("corta")]          // 5 caracteres
    [InlineData("soloLetras")]     // sin números
    [InlineData("12345678")]       // sin letras
    [InlineData("")]
    public void Contrasena_Debil_SeRechaza(string contrasena)
    {
        Assert.Throws<ArgumentException>(() => AdminUsuarioRepositorio.ValidarContrasena(contrasena));
    }

    [Theory]
    [InlineData("Gente.Fit1")]
    [InlineData("recepcion2026")]
    public void Contrasena_Valida_SeAcepta(string contrasena)
    {
        AdminUsuarioRepositorio.ValidarContrasena(contrasena); // no lanza excepción
    }

    [Theory]
    [InlineData("sin-arroba.com")]
    [InlineData("dos@@gentefit.test")]
    [InlineData("Nombre <a@b.com>")]
    [InlineData("")]
    public void Email_Incorrecto_SeRechaza(string email)
    {
        Assert.Throws<ArgumentException>(() => AdminUsuarioRepositorio.ValidarEmail(email));
    }

    [Fact]
    public void Email_SeGuardaSinEspacios()
    {
        Assert.Equal("ana@gentefit.test", AdminUsuarioRepositorio.ValidarEmail("  ana@gentefit.test "));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("con espacio")]
    [InlineData("acentuado#")]
    public void NombreUsuario_Incorrecto_SeRechaza(string nombre)
    {
        Assert.Throws<ArgumentException>(() => AdminUsuarioRepositorio.ValidarNombreUsuario(nombre));
    }

    [Theory]
    [InlineData("laura.recepcion")]
    [InlineData("encargado_2")]
    public void NombreUsuario_Correcto_SeAcepta(string nombre)
    {
        Assert.Equal(nombre, AdminUsuarioRepositorio.ValidarNombreUsuario(nombre));
    }
}
