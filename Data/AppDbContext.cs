using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TrazeaBlock.Models;

namespace TrazeaBlock.Data;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Organizacion> Organizaciones => Set<Organizacion>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<Hito> Hitos => Set<Hito>();
    public DbSet<Donacion> Donaciones => Set<Donacion>();
    public DbSet<Evidencia> Evidencias => Set<Evidencia>();
    public DbSet<Comunicado> Comunicados => Set<Comunicado>();
    public DbSet<OrgMercadoPago> OrgMercadoPagos => Set<OrgMercadoPago>();
    public DbSet<DonacionPendiente> DonacionesPendientes => Set<DonacionPendiente>();
    public DbSet<PadronOng> PadronOngs => Set<PadronOng>();

    // Claves con las que ASP.NET cifra las cookies de sesión. Van en la base porque el
    // hosting no tiene disco persistente: si se perdieran en cada reinicio, todos los
    // usuarios quedarían deslogueados.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Decimales con precisión monetaria
        modelBuilder.Entity<Proyecto>().Property(p => p.MontoObjetivo).HasPrecision(18, 2);
        modelBuilder.Entity<Proyecto>().Property(p => p.MontoRecaudado).HasPrecision(18, 2);
        modelBuilder.Entity<Hito>().Property(h => h.MontoReferencia).HasPrecision(18, 2);
        modelBuilder.Entity<Donacion>().Property(d => d.Monto).HasPrecision(18, 2);

        // Propiedad calculada no mapeada
        modelBuilder.Entity<Proyecto>().Ignore(p => p.PorcentajeAvance);

        // Índices únicos
        modelBuilder.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Donacion>().HasIndex(d => d.TokenId).IsUnique();
        // Un pago de MercadoPago confirma una sola donación. Las del seed no tienen pago.
        modelBuilder.Entity<Donacion>().HasIndex(d => d.PaymentId).IsUnique().HasFilter("\"PaymentId\" <> ''");

        // Relación 1-1 Usuario <-> Organizacion
        modelBuilder.Entity<Organizacion>()
            .HasOne(o => o.Usuario)
            .WithOne(u => u.Organizacion)
            .HasForeignKey<Organizacion>(o => o.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Organizacion 1-* Proyecto
        modelBuilder.Entity<Proyecto>()
            .HasOne(p => p.Organizacion)
            .WithMany(o => o.Proyectos)
            .HasForeignKey(p => p.OrganizacionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Proyecto 1-* Donacion
        modelBuilder.Entity<Donacion>()
            .HasOne(d => d.Proyecto)
            .WithMany(p => p.Donaciones)
            .HasForeignKey(d => d.ProyectoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Usuario 1-* Donacion (Restrict para evitar múltiples rutas de cascada)
        modelBuilder.Entity<Donacion>()
            .HasOne(d => d.Usuario)
            .WithMany(u => u.Donaciones)
            .HasForeignKey(d => d.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Proyecto 1-* Hito / Evidencia / Comunicado
        modelBuilder.Entity<Hito>()
            .HasOne(h => h.Proyecto)
            .WithMany(p => p.Hitos)
            .HasForeignKey(h => h.ProyectoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Evidencia>()
            .HasOne(e => e.Proyecto)
            .WithMany(p => p.Evidencias)
            .HasForeignKey(e => e.ProyectoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Comunicado>()
            .HasOne(c => c.Proyecto)
            .WithMany(p => p.Comunicados)
            .HasForeignKey(c => c.ProyectoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Conexión MP por organización (1-1), con índice único
        modelBuilder.Entity<OrgMercadoPago>()
            .HasOne(m => m.Organizacion)
            .WithMany()
            .HasForeignKey(m => m.OrganizacionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<OrgMercadoPago>()
            .HasIndex(m => m.OrganizacionId).IsUnique();
        // Una cuenta de MercadoPago cobra para una sola ONG.
        modelBuilder.Entity<OrgMercadoPago>()
            .HasIndex(m => m.MpUserId).IsUnique();

        modelBuilder.Entity<DonacionPendiente>().Property(d => d.Monto).HasPrecision(18, 2);
        modelBuilder.Entity<DonacionPendiente>().HasIndex(d => d.TokenId).IsUnique();

        // Defaults en la base para que los registros anteriores a la blockchain real
        // queden marcados como simulados (y no con string vacío) al migrar.
        modelBuilder.Entity<Donacion>().Property(d => d.RedBlockchain).HasDefaultValue("mock");
        modelBuilder.Entity<Evidencia>().Property(e => e.RedBlockchain).HasDefaultValue("mock");
        modelBuilder.Entity<Evidencia>().Property(e => e.EstadoBlockchain).HasDefaultValue("RegistradoBlockchain");

        // Padrón oficial de ONGs: búsqueda por CUIT, una fila por CUIT
        modelBuilder.Entity<PadronOng>().Property(p => p.Cuit).HasMaxLength(11);
        modelBuilder.Entity<PadronOng>().HasIndex(p => p.Cuit).IsUnique();

        // Postgres (timestamptz) exige DateTime con Kind=Utc. Se normalizan todas las fechas:
        // al escribir pasan a UTC (las Unspecified de los <input type="date"> se toman como UTC
        // para no correr el día) y al leer vuelven marcadas como Utc.
        var utc = new ValueConverter<DateTime, DateTime>(
            v => ToUtc(v),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNullable = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? ToUtc(v.Value) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            if (property.ClrType == typeof(DateTime)) property.SetValueConverter(utc);
            else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(utcNullable);
        }
    }

    private static DateTime ToUtc(DateTime v) => v.Kind switch
    {
        DateTimeKind.Utc => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
    };
}
