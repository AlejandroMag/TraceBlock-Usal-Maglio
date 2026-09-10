namespace TrazeaBlock.Configuration;

// Credenciales de MercadoPago.
// - PublicKey / AccessToken: credenciales de PRUEBA de la plataforma (cobro con cuenta única / mock).
// - ClientId / ClientSecret / RedirectUri: OAuth (Marketplace) para que cada ONG conecte SU cuenta.
// Los secretos (AccessToken, ClientSecret) van por user-secrets, nunca en el repo.
public class MercadoPagoOptions
{
    public string PublicKey { get; set; } = "";
    public string AccessToken { get; set; } = "";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
}
