
using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Seguridad;

namespace GenteFit.Persistencia.Datos;

// Datos de ejemplo exclusivos para desarrollo.
public static class DatosDemoCatalogo
{
    public static void Sembrar(GenteFitDbContext contexto)
    {
        // Evitamos duplicar los datos al reiniciar.
        if (contexto.Usuarios.Any(u =>
            u.NombreUsuario == "monitor_catalogo_demo"))
        {
            return;
        }

        var usuarioMonitor = new Usuario
        {
            NombreUsuario = "monitor_catalogo_demo",
            Email = "monitor.catalogo@gentefit.test",
            HashContrasena = HashContrasena.Crear(
                DatosIniciales.ContrasenaPrueba),
            Rol = Rol.MONITOR,
            Activo = true
        };

        var monitor = new GenteFit.Persistencia.Entidades.Monitor
        {
            Dni = "99999001Z",
            Nombre = "Laura",
            Apellidos = "Martínez",
            Email = usuarioMonitor.Email,
            Telefono = "600000001",
            Especialidad = "Actividades dirigidas",
            Activo = true,
            Usuario = usuarioMonitor
        };

        var sala = new Sala
        {
            Nombre = "DEMO - Sala 1",
            Ubicacion = "Planta baja",
            Activa = true
        };

        var yoga = new Actividad
        {
            Nombre = "DEMO - Yoga suave",
            Descripcion = "Relajación, movilidad y equilibrio.",
            Intensidad = NivelIntensidad.BAJA,
            DuracionMinutos = 60,
            Activa = true
        };

        var zumba = new Actividad
        {
            Nombre = "DEMO - Zumba",
            Descripcion = "Baile y ejercicio cardiovascular.",
            Intensidad = NivelIntensidad.ALTA,
            DuracionMinutos = 60,
            Activa = true
        };

        var pilates = new Actividad
        {
            Nombre = "DEMO - Pilates",
            Descripcion = "Fortalecimiento y control postural.",
            Intensidad = NivelIntensidad.MEDIA,
            DuracionMinutos = 60,
            Activa = true
        };

        DateTime manana = DateTime.Today.AddDays(1);

        contexto.Sesiones.AddRange(
            new Sesion
            {
                Actividad = yoga,
                Sala = sala,
                Monitor = monitor,
                FechaHoraInicio = manana.AddHours(18),
                FechaHoraFin = manana.AddHours(19),
                AforoMaximo = 16
            },
            new Sesion
            {
                Actividad = zumba,
                Sala = sala,
                Monitor = monitor,
                FechaHoraInicio = manana.AddDays(1).AddHours(19),
                FechaHoraFin = manana.AddDays(1).AddHours(20),
                AforoMaximo = 16
            },
            new Sesion
            {
                Actividad = pilates,
                Sala = sala,
                Monitor = monitor,
                FechaHoraInicio = manana.AddDays(2).AddHours(10),
                FechaHoraFin = manana.AddDays(2).AddHours(11),
                AforoMaximo = 16
            }
        );

        contexto.SaveChanges();
    }
}
