using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace GenteFit.Persistencia.Datos;

// Prepara la base de datos en una instalación nueva. Lo ejecuta el instalador
// (GenteFit.web.exe --preparar-bd ...) y también se puede lanzar a mano para reparar.
//  1. Crea la base de datos si no existe y aplica las migraciones pendientes.
//  2. Si no hay ningún administrador, crea uno con el email y la contraseña indicados.
//  3. Opcionalmente, carga los datos de demostración.
public static class PreparacionBaseDatos
{
    public static List<string> Preparar(
        GenteFitDbContext contexto,
        string? emailAdministrador,
        string? contrasenaAdministrador,
        bool cargarDatosDemo)
    {
        var pasos = new List<string>();

        IEnumerable<string> pendientes = contexto.Database.GetPendingMigrations().ToList();
        contexto.Database.Migrate();
        pasos.Add(pendientes.Any()
            ? $"Base de datos actualizada ({pendientes.Count()} migraciones aplicadas)."
            : "La base de datos ya estaba actualizada.");

        // Los datos de demostración van primero: DatosIniciales solo actúa con la tabla de usuarios vacía.
        if (cargarDatosDemo)
        {
            DatosIniciales.Sembrar(contexto);
            DatosDemoCatalogo.Sembrar(contexto);
            DatosDemoInformes.Sembrar(contexto);
            pasos.Add("Datos de demostración cargados.");
        }

        if (!string.IsNullOrWhiteSpace(emailAdministrador) && !string.IsNullOrEmpty(contrasenaAdministrador))
        {
            string email = emailAdministrador.Trim();

            if (contexto.Usuarios.Any(u => u.Email == email))
            {
                pasos.Add($"Ya existe una cuenta con el email {email}: no se crea de nuevo.");
            }
            else
            {
                string nombre = "admin";
                for (int i = 2; contexto.Usuarios.Any(u => u.NombreUsuario == nombre); i++)
                    nombre = $"admin{i}";

                new AdminUsuarioRepositorio(contexto).Crear(nombre, email, contrasenaAdministrador, Rol.ADMIN);
                pasos.Add($"Administrador creado: {email} (usuario «{nombre}»).");
            }
        }
        else if (!contexto.Usuarios.Any(u => u.Rol == Rol.ADMIN && u.Activo))
        {
            pasos.Add("AVISO: no hay ningún administrador y no se ha indicado ninguno.");
        }

        return pasos;
    }
}
