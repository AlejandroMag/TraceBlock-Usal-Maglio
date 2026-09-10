namespace TrazeaBlock.Models;

public class Organizacion
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string NombreOrganizacion { get; set; } = "";
    public string CUIT { get; set; } = "";
    public string EmailInstitucional { get; set; } = "";
    public EstadoValidacion Estado { get; set; } = EstadoValidacion.Pendiente;
    public DateTime? FechaValidacion { get; set; }
    public string? HashPdfCenoc { get; set; }
    public string? UrlPdfCenoc { get; set; }
    public string Descripcion { get; set; } = "";
    public double? LatitudSede { get; set; }
    public double? LongitudSede { get; set; }
    public string CiudadSede { get; set; } = "";
    public List<Proyecto> Proyectos { get; set; } = new();
}

public enum EstadoValidacion { Pendiente, Validada, Rechazada }
