namespace TrazeaBlock.Models;

public class Hito
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto Proyecto { get; set; } = null!;
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal? MontoReferencia { get; set; }
    public DateTime? FechaObjetivo { get; set; }
    public bool Completado { get; set; } = false;
    public DateTime? FechaCompletado { get; set; }
    public int Orden { get; set; } = 0;
}
