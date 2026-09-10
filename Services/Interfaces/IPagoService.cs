namespace TrazeaBlock.Services.Interfaces;

public interface IPagoService
{
    Task<PagoResult> ProcesarPago(PagoRequest req);
}

// Datos del pago. CardToken/PaymentMethodId/Installments provienen del Brick de
// MercadoPago (frontend). El servicio mock los ignora.
public record PagoRequest(
    decimal Monto,
    string Email,
    string TokenId,
    string? CardToken = null,
    string? PaymentMethodId = null,
    int Installments = 1,
    string? AccessTokenOng = null);

public record PagoResult(bool Exitoso, string PaymentId, string? Error);
