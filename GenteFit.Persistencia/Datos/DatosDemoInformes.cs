using GenteFit.Persistencia.Entidades;
using GenteFit.Persistencia.Seguridad;

namespace GenteFit.Persistencia.Datos;

// Datos de demostración para que los informes del administrador tengan contenido:
// clientes dados de alta a lo largo del año y sesiones de las últimas semanas con reservas.
// Se usa en desarrollo y, si se marca la opción, en la instalación de demostración.
// Todo lo creado lleva el prefijo "DEMO" o el dominio @demo.gentefit.test para reconocerlo.
public static class DatosDemoInformes
{
    private const string Marcador = "monitor_informes_demo";
    private const string LetrasDni = "TRWAGMYFPDXBNJZSQVHLCKE";

    private static readonly string[] Nombres =
    {
        "Lucía", "Marc", "Paula", "Jordi", "Elena", "Sergio", "Marta", "Pol", "Nuria", "Iván",
        "Laia", "Hugo", "Sara", "Àlex", "Claudia", "David", "Irene", "Óscar", "Noa", "Raúl"
    };

    private static readonly string[] Apellidos =
    {
        "García López", "Puig Serra", "Martín Ruiz", "Ferrer Soler", "Sánchez Gil",
        "Vidal Roca", "Romero Díaz", "Font Casas", "Navarro Peña", "Muñoz Ortega"
    };

    public static void Sembrar(GenteFitDbContext contexto)
    {
        if (contexto.Usuarios.Any(u => u.NombreUsuario == Marcador))
            return;

        var aleatorio = new Random(59); // semilla fija: siempre los mismos datos
        string hash = HashContrasena.Crear(DatosIniciales.ContrasenaPrueba);
        DateTime hoy = DateTime.Today;

        // ---------- Monitor, salas y actividades ----------
        var usuarioMonitor = new Usuario
        {
            NombreUsuario = Marcador,
            Email = "monitor.informes@demo.gentefit.test",
            HashContrasena = hash,
            Rol = Rol.MONITOR
        };

        var monitor = new GenteFit.Persistencia.Entidades.Monitor
        {
            Dni = Dni(70000001),
            Nombre = "Andrea",
            Apellidos = "Costa Pujol",
            Email = usuarioMonitor.Email,
            Telefono = "600111222",
            Especialidad = "Fitness y ciclo indoor",
            Usuario = usuarioMonitor
        };

        Sala[] salas =
        {
            new() { Nombre = "DEMO - Sala Ciclo", Ubicacion = "Planta 1" },
            new() { Nombre = "DEMO - Sala Polivalente", Ubicacion = "Planta baja" },
            new() { Nombre = "DEMO - Sala Zen", Ubicacion = "Planta 2" }
        };

        Actividad[] actividades =
        {
            new() { Nombre = "DEMO - Spinning", Descripcion = "Ciclo indoor por intervalos.", Intensidad = NivelIntensidad.ALTA, DuracionMinutos = 45 },
            new() { Nombre = "DEMO - Body Pump", Descripcion = "Tonificación con barra y discos.", Intensidad = NivelIntensidad.ALTA, DuracionMinutos = 55 },
            new() { Nombre = "DEMO - Pilates suelo", Descripcion = "Control postural y core.", Intensidad = NivelIntensidad.MEDIA, DuracionMinutos = 50 },
            new() { Nombre = "DEMO - Yoga", Descripcion = "Flexibilidad, respiración y relajación.", Intensidad = NivelIntensidad.BAJA, DuracionMinutos = 60 },
            new() { Nombre = "DEMO - Zumba", Descripcion = "Baile y cardio.", Intensidad = NivelIntensidad.MEDIA, DuracionMinutos = 50 }
        };

        // Popularidad de cada actividad (probabilidad de que un cliente reserve).
        double[] popularidad = { 0.95, 0.8, 0.6, 0.45, 0.7 };
        int[] salaDeActividad = { 0, 1, 2, 2, 1 };

        // ---------- Clientes (altas repartidas en los últimos 10 meses) ----------
        var clientes = new List<Cliente>();

        for (int i = 0; i < 20; i++)
        {
            string nombre = Nombres[i];
            string email = $"cliente{i + 1:00}@demo.gentefit.test";

            var usuario = new Usuario
            {
                NombreUsuario = $"demo_cliente{i + 1:00}",
                Email = email,
                HashContrasena = hash,
                Rol = Rol.CLIENTE
            };

            clientes.Add(new Cliente
            {
                Dni = Dni(71000000 + i * 37),
                Nombre = nombre,
                Apellidos = Apellidos[i % Apellidos.Length],
                Email = email,
                Telefono = $"6{aleatorio.Next(10000000, 99999999)}",
                FechaNacimiento = new DateTime(1970 + aleatorio.Next(0, 35), aleatorio.Next(1, 13), aleatorio.Next(1, 29)),
                FechaAlta = hoy.AddDays(-aleatorio.Next(0, 300)),
                Activo = i != 7, // un cliente dado de baja
                Usuario = usuario
            });
        }

        contexto.Clientes.AddRange(clientes);

        // ---------- Sesiones: 4 semanas pasadas y la semana que viene ----------
        // Lunes a viernes: dos sesiones por la tarde y una por la mañana.
        (int Hora, int Actividad)[] horario = { (9, 3), (18, 0), (19, 1) };
        (int Hora, int Actividad)[] horarioAlterno = { (10, 2), (18, 4), (19, 0) };

        DateTime lunes = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7)).AddDays(-28);

        for (int dia = 0; dia < 35; dia++)
        {
            DateTime fecha = lunes.AddDays(dia);
            if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            var franjas = fecha.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Thursday ? horarioAlterno : horario;

            foreach (var (hora, indiceActividad) in franjas)
            {
                Actividad actividad = actividades[indiceActividad];
                DateTime inicio = fecha.AddHours(hora);

                var sesion = new Sesion
                {
                    Actividad = actividad,
                    Sala = salas[salaDeActividad[indiceActividad]],
                    Monitor = monitor,
                    FechaHoraInicio = inicio,
                    FechaHoraFin = inicio.AddMinutes(actividad.DuracionMinutos),
                    AforoMaximo = 16
                };

                bool futura = inicio > DateTime.Now;
                double probabilidad = popularidad[indiceActividad] * (futura ? 0.5 : 1);

                // Cada cliente activo reserva como mucho una vez en cada sesión.
                int confirmadas = 0;
                foreach (Cliente cliente in clientes.Where(c => c.Activo).OrderBy(_ => aleatorio.Next()))
                {
                    if (aleatorio.NextDouble() > probabilidad)
                        continue;

                    var reserva = new Reserva
                    {
                        Cliente = cliente,
                        Sesion = sesion,
                        FechaHoraReserva = inicio.AddDays(-aleatorio.Next(1, 6)).AddHours(-aleatorio.Next(0, 10))
                    };

                    if (!futura && aleatorio.NextDouble() < 0.08)
                    {
                        reserva.Estado = EstadoReserva.ANULADA;
                        reserva.FechaHoraAnulacion = inicio.AddHours(-aleatorio.Next(3, 30));
                    }
                    else if (confirmadas < sesion.AforoMaximo)
                    {
                        reserva.Estado = EstadoReserva.CONFIRMADA;
                        confirmadas++;
                    }
                    else
                    {
                        reserva.Estado = EstadoReserva.EN_ESPERA;
                    }

                    sesion.Reservas.Add(reserva);
                }

                contexto.Sesiones.Add(sesion);
            }
        }

        contexto.SaveChanges();
    }

    // DNI con la letra de control correcta.
    private static string Dni(int numero) => $"{numero:00000000}{LetrasDni[numero % 23]}";
}
