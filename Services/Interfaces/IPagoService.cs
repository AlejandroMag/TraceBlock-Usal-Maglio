namespace TrazeaBlock.Services.Interfaces;

/// <summary>
/// Cobro de respaldo para correr la app sin credenciales de MercadoPago
/// (Validaciones:ValidarPagoMercadoPago = false). No contacta a ningún proveedor.
///
/// El cobro de la plataforma NO pasa por acá: va por Checkout Pro
/// (<see cref="MercadoPago.MercadoPagoConexionService"/>), que redirige al donante a
/// MercadoPago y acredita en la cuenta de la ONG. Que las credenciales sean de prueba
/// no lo vuelve ficticio: el pago lo procesa MercadoPago y devuelve un payment_id real.
/// </summary>
public interface IPagoService
{
    Task<PagoResult> ProcesarPago(PagoRequest req);
}

public record PagoRequest(decimal Monto, string Email, string TokenId);

public record PagoResult(bool Exitoso, string PaymentId, string? Error);
