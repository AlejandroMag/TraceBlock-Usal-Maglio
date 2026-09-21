namespace TrazeaBlock.Models;

public class Donacion
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto Proyecto { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string TokenId { get; set; } = "";
    public decimal Monto { get; set; }
    public string? Mensaje { get; set; }
    public bool Anonima { get; set; } = false;
    public DateTime FechaDonacion { get; set; } = DateTime.UtcNow;
    public string TxHashBlockchain { get; set; } = "";
    // "RegistradoBlockchain" cuando la transacción está confirmada;
    // "PendienteBlockchain" si aún no se pudo anclar (el servicio de fondo lo reintenta).
    public string Estado { get; set; } = "RegistradoBlockchain";
    // Red donde se registró el hash: "mock" (simulado) o la testnet real (ej. "amoy").
    public string RedBlockchain { get; set; } = "mock";
    // Id del pago en MercadoPago (vacío cuando se usa el pago simulado)
    public string PaymentId { get; set; } = "";
}
