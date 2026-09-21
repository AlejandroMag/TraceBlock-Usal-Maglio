namespace TrazeaBlock.Models;

public class Evidencia
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto Proyecto { get; set; } = null!;
    public string NombreArchivo { get; set; } = "";
    public string HashSHA256 { get; set; } = "";
    public string TxHashBlockchain { get; set; } = "";
    // Igual que en Donacion: "RegistradoBlockchain" o "PendienteBlockchain".
    public string EstadoBlockchain { get; set; } = "RegistradoBlockchain";
    // Red donde se registró el hash: "mock" o la testnet real (ej. "amoy").
    public string RedBlockchain { get; set; } = "mock";
    public string Descripcion { get; set; } = "";
    public string TipoEvidencia { get; set; } = "documento";
    public long TamanoBytes { get; set; }
    public DateTime SubidoEn { get; set; } = DateTime.UtcNow;
}
