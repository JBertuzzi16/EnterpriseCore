using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Excepciones;
using GenteFit.Persistencia.Repositorios;

namespace GenteFit.TestsAdmin;

// Gestión de usuarios y roles desde el panel del administrador (con SQL Server).
public class AdminUsuarioRepositorioTests : BaseDatosPrueba
{
    private readonly AdminUsuarioRepositorio _repositorio;
    private readonly Usuario _admin;

    public AdminUsuarioRepositorioTests()
    {
        _repositorio = new AdminUsuarioRepositorio(Contexto);
        _admin = CrearUsuario("admin", Rol.ADMIN);
    }

    [Fact]
    public void Crear_DatosCorrectos_GuardaUsuarioConContrasenaCifrada()
    {
        _repositorio.Crear("laura.recepcion", "laura@gentefit.test", "Recepcion2026", Rol.RECEPCIONISTA);

        using var comprobacion = ContextoNuevo();
        Usuario guardado = comprobacion.Usuarios.Single(u => u.NombreUsuario == "laura.recepcion");

        Assert.Equal(Rol.RECEPCIONISTA, guardado.Rol);
        Assert.True(guardado.Activo);
        Assert.NotEqual("Recepcion2026", guardado.HashContrasena);
        Assert.StartsWith("PBKDF2$", guardado.HashContrasena);
    }

    [Fact]
    public void Crear_UsuarioCreado_PuedeIniciarSesion()
    {
        _repositorio.Crear("pedro", "pedro@gentefit.test", "Encargado1", Rol.ENCARGADO);

        Usuario? login = new UsuarioRepositorio(ContextoNuevo()).ValidarCredenciales("pedro@gentefit.test", "Encargado1");

        Assert.NotNull(login);
        Assert.Equal(Rol.ENCARGADO, login.Rol);
    }

    [Fact]
    public void Crear_NombreRepetido_SeRechaza()
    {
        var error = Assert.Throws<GenteFitException>(() =>
            _repositorio.Crear("admin", "otro@gentefit.test", "Gente.Fit1", Rol.RECEPCIONISTA));

        Assert.Contains("nombre", error.Message);
    }

    [Fact]
    public void Crear_EmailRepetido_SeRechaza()
    {
        var error = Assert.Throws<GenteFitException>(() =>
            _repositorio.Crear("nuevo", "admin@gentefit.test", "Gente.Fit1", Rol.RECEPCIONISTA));

        Assert.Contains("email", error.Message);
    }

    [Theory]
    [InlineData(Rol.CLIENTE)]
    [InlineData(Rol.MONITOR)]
    public void Crear_RolConFicha_NoSePermiteDesdeAdministracion(Rol rol)
    {
        Assert.Throws<GenteFitException>(() =>
            _repositorio.Crear("sinficha", "sinficha@gentefit.test", "Gente.Fit1", rol));
    }

    [Fact]
    public void Editar_CambiaRolDeOtroUsuario()
    {
        Usuario recepcion = CrearUsuario("recepcion", Rol.RECEPCIONISTA);

        _repositorio.Editar(recepcion.Id, "recepcion", "recepcion@gentefit.test", Rol.ENCARGADO, _admin.Id);

        using var comprobacion = ContextoNuevo();
        Assert.Equal(Rol.ENCARGADO, comprobacion.Usuarios.Find(recepcion.Id)!.Rol);
    }

    [Fact]
    public void Editar_NoPuedeCambiarSuPropioRol()
    {
        Assert.Throws<GenteFitException>(() =>
            _repositorio.Editar(_admin.Id, "admin", "admin@gentefit.test", Rol.RECEPCIONISTA, _admin.Id));
    }

    [Fact]
    public void Editar_CuentaConFichaDeCliente_NoCambiaDeRol()
    {
        Usuario usuarioCliente = CrearUsuario("cliente", Rol.CLIENTE);
        Contexto.Clientes.Add(new Cliente
        {
            Dni = "12345678Z",
            Nombre = "Carlos",
            Apellidos = "Mendoza",
            Email = usuarioCliente.Email,
            Telefono = "600000000",
            FechaNacimiento = new DateTime(1995, 5, 20),
            FechaAlta = DateTime.Today,
            UsuarioId = usuarioCliente.Id
        });
        Contexto.SaveChanges();

        Assert.Throws<GenteFitException>(() =>
            _repositorio.Editar(usuarioCliente.Id, "cliente", usuarioCliente.Email, Rol.ENCARGADO, _admin.Id));
    }

    [Fact]
    public void Desactivar_UsuarioDesactivado_NoPuedeIniciarSesion()
    {
        Usuario recepcion = CrearUsuario("recepcion", Rol.RECEPCIONISTA);

        _repositorio.Desactivar(recepcion.Id, _admin.Id);

        Usuario? login = new UsuarioRepositorio(ContextoNuevo()).ValidarCredenciales("recepcion@gentefit.test", "Gente.Fit1");
        Assert.Null(login);
    }

    [Fact]
    public void Desactivar_SuPropiaCuenta_SeRechaza()
    {
        Assert.Throws<GenteFitException>(() => _repositorio.Desactivar(_admin.Id, _admin.Id));
    }

    [Fact]
    public void Desactivar_UltimoAdministrador_SeRechaza()
    {
        // Otro administrador (desactivado) intenta dejar el sistema sin administradores activos.
        Usuario otroAdmin = CrearUsuario("admin2", Rol.ADMIN);

        _repositorio.Desactivar(otroAdmin.Id, _admin.Id); // quedan: admin (activo)

        var error = Assert.Throws<GenteFitException>(() => _repositorio.Desactivar(_admin.Id, otroAdmin.Id));
        Assert.Contains("al menos un administrador", error.Message);
    }

    [Fact]
    public void Activar_DevuelveElAcceso()
    {
        Usuario recepcion = CrearUsuario("recepcion", Rol.RECEPCIONISTA, activo: false);

        _repositorio.Activar(recepcion.Id);

        Assert.NotNull(new UsuarioRepositorio(ContextoNuevo()).ValidarCredenciales("recepcion@gentefit.test", "Gente.Fit1"));
    }

    [Fact]
    public void RestablecerContrasena_LaAnteriorDejaDeFuncionar()
    {
        Usuario recepcion = CrearUsuario("recepcion", Rol.RECEPCIONISTA);

        _repositorio.RestablecerContrasena(recepcion.Id, "NuevaClave99");

        var login = new UsuarioRepositorio(ContextoNuevo());
        Assert.Null(login.ValidarCredenciales("recepcion@gentefit.test", "Gente.Fit1"));
        Assert.NotNull(login.ValidarCredenciales("recepcion@gentefit.test", "NuevaClave99"));
    }

    [Fact]
    public void Listar_FiltraPorRolYEstado()
    {
        CrearUsuario("recepcion1", Rol.RECEPCIONISTA);
        CrearUsuario("recepcion2", Rol.RECEPCIONISTA, activo: false);
        CrearUsuario("encargado", Rol.ENCARGADO);

        Assert.Equal(2, _repositorio.Listar(rol: Rol.RECEPCIONISTA).Count);
        Assert.Single(_repositorio.Listar(rol: Rol.RECEPCIONISTA, activo: true));
        Assert.Single(_repositorio.Listar(texto: "encarg"));
    }
}
