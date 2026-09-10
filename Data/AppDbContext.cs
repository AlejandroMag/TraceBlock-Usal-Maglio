using Microsoft.EntityFrameworkCore;
using TrazeaBlock.Models;

namespace TrazeaBlock.Data;

public class AppDbContext : DbContext
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Decimales con precisión monetaria
        modelBuilder.Entity<Proyecto>().Property(p => p.MontoObjetivo).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Proyecto>().Property(p => p.MontoRecaudado).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Hito>().Property(h => h.MontoReferencia).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Donacion>().Property(d => d.Monto).HasColumnType("decimal(18,2)");

        // Propiedad calculada no mapeada
        modelBuilder.Entity<Proyecto>().Ignore(p => p.PorcentajeAvance);

        // Índices únicos
        modelBuilder.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Donacion>().HasIndex(d => d.TokenId).IsUnique();

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

        modelBuilder.Entity<DonacionPendiente>().Property(d => d.Monto).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<DonacionPendiente>().HasIndex(d => d.TokenId).IsUnique();
    }
}
