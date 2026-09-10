using System.Security.Cryptography;
using System.Text;
using TrazeaBlock.Models;

namespace TrazeaBlock.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Usuarios.Any()) return;

        string Hash(string p) => BCrypt.Net.BCrypt.HashPassword(p);

        // ---- USUARIOS ----
        var donante1 = new Usuario { Email = "donante1@demo.com", PasswordHash = Hash("Demo1234"), Nombre = "Laura", Apellido = "González", Rol = RolUsuario.Donante };
        var donante2 = new Usuario { Email = "donante2@demo.com", PasswordHash = Hash("Demo1234"), Nombre = "Carlos", Apellido = "Rodríguez", Rol = RolUsuario.Donante };
        var orgUser1 = new Usuario { Email = "contacto@alimentar.org.ar", PasswordHash = Hash("Demo1234"), Nombre = "Alimentar", Apellido = "Argentina", Rol = RolUsuario.Organizacion };
        var orgUser2 = new Usuario { Email = "info@techodigital.org.ar", PasswordHash = Hash("Demo1234"), Nombre = "Techo", Apellido = "Digital", Rol = RolUsuario.Organizacion };
        db.Usuarios.AddRange(donante1, donante2, orgUser1, orgUser2);
        db.SaveChanges();

        // ---- ORGANIZACIONES ----
        var org1 = new Organizacion
        {
            UsuarioId = orgUser1.Id,
            NombreOrganizacion = "Alimentar Argentina",
            CUIT = "30-12345678-9",
            EmailInstitucional = "contacto@alimentar.org.ar",
            Estado = EstadoValidacion.Validada,
            FechaValidacion = DateTime.UtcNow.AddDays(-40),
            Descripcion = "Organización dedicada a combatir el hambre en comunidades vulnerables de Argentina mediante comedores y distribución de alimentos.",
            CiudadSede = "Buenos Aires",
            LatitudSede = -34.6037,
            LongitudSede = -58.3816
        };
        var org2 = new Organizacion
        {
            UsuarioId = orgUser2.Id,
            NombreOrganizacion = "Techo Digital",
            CUIT = "30-98765432-1",
            EmailInstitucional = "info@techodigital.org.ar",
            Estado = EstadoValidacion.Validada,
            FechaValidacion = DateTime.UtcNow.AddDays(-30),
            Descripcion = "Llevamos conectividad y equipamiento tecnológico a escuelas rurales del Noroeste Argentino.",
            CiudadSede = "Córdoba",
            LatitudSede = -31.4201,
            LongitudSede = -64.1888
        };
        db.Organizaciones.AddRange(org1, org2);
        db.SaveChanges();

        // ---- PROYECTOS ----
        var p1 = new Proyecto
        {
            OrganizacionId = org1.Id,
            Titulo = "Comedor San Martín – Pilar",
            Descripcion = "Sostenemos el comedor comunitario del barrio San Martín en Pilar, que brinda más de 200 raciones diarias a familias del barrio. Con esta campaña buscamos garantizar las compras de alimentos y equipar la cocina.",
            Categoria = "Alimentación",
            MontoObjetivo = 150000,
            MontoRecaudado = 87500,
            Estado = EstadoProyecto.Activo,
            FechaObjetivo = DateTime.UtcNow.AddMonths(3),
            CiudadProyecto = "Pilar",
            LatitudProyecto = -34.4583,
            LongitudProyecto = -58.9136
        };
        var p2 = new Proyecto
        {
            OrganizacionId = org1.Id,
            Titulo = "Meriendas Primera Infancia",
            Descripcion = "Programa de meriendas nutritivas para niños y niñas de 1 a 5 años en jardines comunitarios. Buscamos asegurar el aporte nutricional diario en la etapa más crítica del desarrollo.",
            Categoria = "Alimentación",
            MontoObjetivo = 80000,
            MontoRecaudado = 12000,
            Estado = EstadoProyecto.Activo,
            FechaObjetivo = DateTime.UtcNow.AddMonths(4),
            CiudadProyecto = "Buenos Aires",
            LatitudProyecto = -34.6037,
            LongitudProyecto = -58.3816
        };
        var p3 = new Proyecto
        {
            OrganizacionId = org2.Id,
            Titulo = "Escuelas Conectadas NOA",
            Descripcion = "Instalamos conectividad satelital y equipamiento informático en escuelas rurales del Noroeste Argentino, beneficiando a más de 1.500 estudiantes que hoy no tienen acceso a internet.",
            Categoria = "Educación",
            MontoObjetivo = 500000,
            MontoRecaudado = 320000,
            Estado = EstadoProyecto.Activo,
            FechaObjetivo = DateTime.UtcNow.AddMonths(6),
            CiudadProyecto = "San Salvador de Jujuy",
            LatitudProyecto = -24.1858,
            LongitudProyecto = -65.2995
        };
        db.Proyectos.AddRange(p1, p2, p3);
        db.SaveChanges();

        // ---- HITOS ----
        db.Hitos.AddRange(
            new Hito { ProyectoId = p1.Id, Titulo = "Compra de alimentos marzo", Descripcion = "Adquisición de alimentos no perecederos para el primer trimestre.", MontoReferencia = 50000, Completado = true, FechaCompletado = DateTime.UtcNow.AddDays(-20), FechaObjetivo = DateTime.UtcNow.AddDays(-25), Orden = 1 },
            new Hito { ProyectoId = p1.Id, Titulo = "Equipamiento cocina", Descripcion = "Compra de heladera industrial y cocina de 6 hornallas.", MontoReferencia = 100000, Completado = false, FechaObjetivo = DateTime.UtcNow.AddMonths(2), Orden = 2 },

            new Hito { ProyectoId = p2.Id, Titulo = "Primera distribución", Descripcion = "Distribución inicial de meriendas en 3 jardines comunitarios.", MontoReferencia = 30000, Completado = false, FechaObjetivo = DateTime.UtcNow.AddMonths(1), Orden = 1 },
            new Hito { ProyectoId = p2.Id, Titulo = "Expansión 5 jardines", Descripcion = "Ampliación del programa a 5 jardines adicionales.", MontoReferencia = 70000, Completado = false, FechaObjetivo = DateTime.UtcNow.AddMonths(3), Orden = 2 },

            new Hito { ProyectoId = p3.Id, Titulo = "Fase 1 – 5 escuelas", Descripcion = "Conectividad y equipamiento en las primeras 5 escuelas.", MontoReferencia = 200000, Completado = true, FechaCompletado = DateTime.UtcNow.AddDays(-15), FechaObjetivo = DateTime.UtcNow.AddDays(-20), Orden = 1 },
            new Hito { ProyectoId = p3.Id, Titulo = "Fase 2 – 10 escuelas", Descripcion = "Expansión a 10 escuelas adicionales del NOA.", MontoReferencia = 450000, Completado = false, FechaObjetivo = DateTime.UtcNow.AddMonths(5), Orden = 2 }
        );

        // ---- DONACIONES ----
        Donacion CrearDonacion(int proyectoId, int usuarioId, decimal monto, string? mensaje, bool anonima, int diasAtras)
        {
            var tokenId = Guid.NewGuid().ToString("N").ToUpper();
            var txHash = "0x" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenId + monto))).ToLower();
            return new Donacion
            {
                ProyectoId = proyectoId,
                UsuarioId = usuarioId,
                TokenId = tokenId,
                Monto = monto,
                Mensaje = mensaje,
                Anonima = anonima,
                FechaDonacion = DateTime.UtcNow.AddDays(-diasAtras),
                TxHashBlockchain = txHash,
                Estado = "RegistradoBlockchain"
            };
        }

        db.Donaciones.AddRange(
            CrearDonacion(p1.Id, donante1.Id, 5000, "¡Gran trabajo! Sigan así.", false, 12),
            CrearDonacion(p1.Id, donante2.Id, 2500, null, true, 8),
            CrearDonacion(p3.Id, donante1.Id, 10000, "Aporto para que más chicos se conecten.", false, 5),
            CrearDonacion(p2.Id, donante2.Id, 1000, "Suerte con el proyecto.", false, 2)
        );

        // ---- EVIDENCIAS ----
        Evidencia CrearEvidencia(int proyectoId)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("seed-evidencia-" + proyectoId))).ToLower();
            return new Evidencia
            {
                ProyectoId = proyectoId,
                NombreArchivo = "factura_compra_alimentos.pdf",
                HashSHA256 = hash,
                TxHashBlockchain = "0x" + hash,
                Descripcion = "Comprobante de compra",
                TipoEvidencia = "Factura",
                TamanoBytes = 184320,
                SubidoEn = DateTime.UtcNow.AddDays(-10)
            };
        }
        db.Evidencias.AddRange(CrearEvidencia(p1.Id), CrearEvidencia(p2.Id), CrearEvidencia(p3.Id));

        // ---- COMUNICADOS ----
        db.Comunicados.AddRange(
            new Comunicado { ProyectoId = p1.Id, Titulo = "Actualización de avance - Semana 1", Cuerpo = "Esta semana logramos servir más de 1.400 raciones en el comedor. Gracias al aporte de la comunidad pudimos sostener el servicio todos los días. ¡Seguimos sumando manos!", FechaPublicacion = DateTime.UtcNow.AddDays(-18), AutorNombre = "Alimentar Argentina" },
            new Comunicado { ProyectoId = p1.Id, Titulo = "¡Llegamos al 50% del objetivo!", Cuerpo = "Con la última tanda de donaciones superamos la mitad de la meta. Ya cerramos la compra de alimentos de marzo y avanzamos con la cocina. Cada peso queda registrado en blockchain.", FechaPublicacion = DateTime.UtcNow.AddDays(-6), AutorNombre = "Alimentar Argentina" },

            new Comunicado { ProyectoId = p2.Id, Titulo = "Arrancamos el programa de meriendas", Cuerpo = "Comenzamos a coordinar con los jardines comunitarios la logística de las primeras entregas. Estamos definiendo el menú nutricional junto a nutricionistas voluntarias.", FechaPublicacion = DateTime.UtcNow.AddDays(-9), AutorNombre = "Alimentar Argentina" },
            new Comunicado { ProyectoId = p2.Id, Titulo = "Primeras familias inscriptas", Cuerpo = "Ya tenemos 45 familias anotadas para recibir las meriendas. Necesitamos seguir recaudando para garantizar la continuidad del programa durante todo el año.", FechaPublicacion = DateTime.UtcNow.AddDays(-3), AutorNombre = "Alimentar Argentina" },

            new Comunicado { ProyectoId = p3.Id, Titulo = "Fase 1 completada: 5 escuelas conectadas", Cuerpo = "¡Lo logramos! Las primeras 5 escuelas rurales ya cuentan con internet satelital y netbooks. Más de 600 estudiantes acceden hoy a contenidos digitales por primera vez.", FechaPublicacion = DateTime.UtcNow.AddDays(-14), AutorNombre = "Techo Digital" },
            new Comunicado { ProyectoId = p3.Id, Titulo = "Comienza la Fase 2", Cuerpo = "Con la Fase 1 cerrada, abrimos la recaudación para las próximas 10 escuelas. La meta es ambiciosa pero el impacto vale la pena. Toda evidencia de compra queda verificable.", FechaPublicacion = DateTime.UtcNow.AddDays(-4), AutorNombre = "Techo Digital" }
        );

        db.SaveChanges();
    }
}
