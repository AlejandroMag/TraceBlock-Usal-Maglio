namespace TrazeaBlock.Models;

public class Comunicado
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto Proyecto { get; set; } = null!;
    public string Titulo { get; set; } = "";
    public string Cuerpo { get; set; } = "";
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
    public string AutorNombre { get; set; } = "";
}
