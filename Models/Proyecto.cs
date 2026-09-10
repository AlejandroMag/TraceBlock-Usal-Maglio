using System.ComponentModel.DataAnnotations.Schema;

namespace TrazeaBlock.Models;

public class Proyecto
{
    public int Id { get; set; }
    public int OrganizacionId { get; set; }
    public Organizacion Organizacion { get; set; } = null!;
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Categoria { get; set; } = "";
    public decimal MontoObjetivo { get; set; }
    public decimal MontoRecaudado { get; set; }
    public EstadoProyecto Estado { get; set; } = EstadoProyecto.Activo;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime FechaObjetivo { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string CiudadProyecto { get; set; } = "";
    public double LatitudProyecto { get; set; }
    public double LongitudProyecto { get; set; }
    public List<Hito> Hitos { get; set; } = new();
    public List<Donacion> Donaciones { get; set; } = new();
    public List<Evidencia> Evidencias { get; set; } = new();
    public List<Comunicado> Comunicados { get; set; } = new();

    [NotMapped]
    public decimal PorcentajeAvance => MontoObjetivo > 0
        ? Math.Min(100, Math.Round(MontoRecaudado / MontoObjetivo * 100, 1)) : 0;
}

public enum EstadoProyecto { Activo, Pausado, Completado }
