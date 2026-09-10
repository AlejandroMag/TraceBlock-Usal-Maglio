namespace TrazeaBlock.Models;

// Intención de donación creada antes de redirigir a Checkout Pro.
// Al volver aprobado, se convierte en una Donacion real y se elimina.
public class DonacionPendiente
{
    public int Id { get; set; }
    public string TokenId { get; set; } = "";
    public int UsuarioId { get; set; }
    public int ProyectoId { get; set; }
    public decimal Monto { get; set; }
    public string? Mensaje { get; set; }
    public bool Anonima { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
