using Microsoft.EntityFrameworkCore;
using TrazeaBlock.Configuration;
using TrazeaBlock.Data;
using TrazeaBlock.Models;
using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services;

public class ProyectoService
{
    private readonly AppDbContext _db;
    private readonly IGeoService _geo;
    private readonly ValidacionesConfig _config;

    public ProyectoService(AppDbContext db, IGeoService geo, ValidacionesConfig config)
    {
        _db = db;
        _geo = geo;
        _config = config;
    }

    public async Task<List<Proyecto>> ObtenerActivosAsync(
        string? categoria = null, string? ciudad = null, string? buscar = null)
    {
        var query = _db.Proyectos
            .Include(p => p.Organizacion)
            .Where(p => p.Estado == EstadoProyecto.Activo);

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p => p.Categoria == categoria);

        if (!string.IsNullOrWhiteSpace(ciudad))
            query = query.Where(p => p.CiudadProyecto.ToLower().Contains(ciudad.ToLower()));

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.ToLower();
            query = query.Where(p => p.Titulo.ToLower().Contains(b) || p.Descripcion.ToLower().Contains(b));
        }

        return await query.OrderByDescending(p => p.FechaCreacion).ToListAsync();
    }

    public async Task<Proyecto?> ObtenerConDetalleAsync(int id)
    {
        return await _db.Proyectos
            .Include(p => p.Organizacion)
            .Include(p => p.Hitos)
            .Include(p => p.Donaciones).ThenInclude(d => d.Usuario)
            .Include(p => p.Evidencias)
            .Include(p => p.Comunicados)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Proyecto>> ObtenerDeOrganizacionAsync(int orgId)
    {
        return await _db.Proyectos
            .Where(p => p.OrganizacionId == orgId)
            .Include(p => p.Hitos)
            .Include(p => p.Donaciones)
            .Include(p => p.Evidencias)
            .Include(p => p.Comunicados)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();
    }

    public async Task<(Proyecto? proyecto, string? error)> CrearProyectoAsync(
        Proyecto proyecto, double latUsuario, double lonUsuario)
    {
        if (proyecto.Hitos is null || proyecto.Hitos.Count == 0)
            return (null, "El proyecto debe tener al menos un hito.");

        if (proyecto.MontoObjetivo <= 0)
            return (null, "El monto objetivo debe ser mayor a cero.");

        if (_config.ValidarGeolocalizacion)
        {
            var geo = await _geo.ValidarDistancia(
                proyecto.LatitudProyecto, proyecto.LongitudProyecto,
                latUsuario, lonUsuario);
            if (!geo.Valido)
                return (null, $"El proyecto está a {geo.DistanciaKm:N1} km de tu ubicación. Debe estar dentro de los {GeoUtils.MaxDistanciaKm:N0} km de tu área.");
        }

        proyecto.FechaCreacion = DateTime.UtcNow;
        proyecto.FechaInicio = DateTime.UtcNow;
        _db.Proyectos.Add(proyecto);
        await _db.SaveChangesAsync();
        return (proyecto, null);
    }

    public async Task MarcarHitoCompletadoAsync(int hitoId)
    {
        var hito = await _db.Hitos.FindAsync(hitoId);
        if (hito is null) return;
        hito.Completado = true;
        hito.FechaCompletado = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<Comunicado> PublicarComunicadoAsync(int proyectoId, string titulo, string cuerpo, string autorNombre)
    {
        var comunicado = new Comunicado
        {
            ProyectoId = proyectoId,
            Titulo = titulo,
            Cuerpo = cuerpo,
            AutorNombre = autorNombre,
            FechaPublicacion = DateTime.UtcNow
        };
        _db.Comunicados.Add(comunicado);
        await _db.SaveChangesAsync();
        return comunicado;
    }
}
