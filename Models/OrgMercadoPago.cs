namespace TrazeaBlock.Models;

// Conexión OAuth de una organización con su propia cuenta de MercadoPago.
// Guarda los tokens que devuelve el intercambio OAuth para cobrar en nombre de la ONG.
public class OrgMercadoPago
{
    public int Id { get; set; }
    public int OrganizacionId { get; set; }
    public Organizacion Organizacion { get; set; } = null!;

    public string MpUserId { get; set; } = "";        // collector id de la ONG en MP
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string PublicKey { get; set; } = "";        // public key de la ONG (para tokenizar la tarjeta)
    public DateTime ExpiraEn { get; set; }             // vencimiento del AccessToken (UTC)
    public DateTime FechaConexion { get; set; } = DateTime.UtcNow;
}
