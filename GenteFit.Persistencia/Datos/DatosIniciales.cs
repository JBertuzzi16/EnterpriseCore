using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Seguridad;

namespace GenteFit.Persistencia.Datos;

// Usuarios de prueba para desarrollo: uno por rol. Solo se crean si la tabla Usuarios está vacía.
// IMPORTANTE: son contraseñas de prueba; no usarlas en una instalación real.
public static class DatosIniciales
{
    public const string ContrasenaPrueba = "Gente.Fit1";

    public static void Sembrar(GenteFitDbContext contexto)
    {
        if (contexto.Usuarios.Any())
            return;

        string hash = HashContrasena.Crear(ContrasenaPrueba);

        contexto.Usuarios.AddRange(
            new Usuario { NombreUsuario = "admin", Email = "admin@gentefit.test", HashContrasena = hash, Rol = Rol.ADMIN },
            new Usuario { NombreUsuario = "recepcion", Email = "recepcion@gentefit.test", HashContrasena = hash, Rol = Rol.RECEPCIONISTA },
            new Usuario { NombreUsuario = "encargado", Email = "encargado@gentefit.test", HashContrasena = hash, Rol = Rol.ENCARGADO });

        var usuarioCliente = new Usuario
        {
            NombreUsuario = "cliente",
            Email = "cliente@gentefit.test",
            HashContrasena = hash,
            Rol = Rol.CLIENTE
        };
        contexto.Usuarios.Add(usuarioCliente);

        contexto.Clientes.Add(new Cliente
        {
            Dni = "12345678Z",
            Nombre = "Carlos",
            Apellidos = "Mendoza García",
            Email = "cliente@gentefit.test",
            Telefono = "+34 612 345 678",
            FechaNacimiento = new DateTime(1995, 5, 20),
            FechaAlta = DateTime.Today,
            Usuario = usuarioCliente
        });

        contexto.SaveChanges();
    }
}
