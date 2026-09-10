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
    public string Estado { get; set; } = "RegistradoBlockchain";
    // Id del pago en MercadoPago (vacío cuando se usa el pago simulado)
    public string PaymentId { get; set; } = "";
}
