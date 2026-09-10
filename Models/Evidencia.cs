namespace TrazeaBlock.Models;

public class Evidencia
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto Proyecto { get; set; } = null!;
    public string NombreArchivo { get; set; } = "";
    public string HashSHA256 { get; set; } = "";
    public string TxHashBlockchain { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string TipoEvidencia { get; set; } = "documento";
    public long TamanoBytes { get; set; }
    public DateTime SubidoEn { get; set; } = DateTime.UtcNow;
}
