using MercadoPago.Client;
using MercadoPago.Client.Payment;
using MercadoPago.Resource.Payment;
using TrazeaBlock.Configuration;
using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.MercadoPago;

// Implementación real contra la API de pagos de MercadoPago (Checkout API).
// Recibe el token de tarjeta generado por el Brick y crea el pago.
public class MercadoPagoPagoService : IPagoService
{
    private readonly MercadoPagoOptions _options;

    public MercadoPagoPagoService(MercadoPagoOptions options)
    {
        _options = options;
        // El SDK usa un AccessToken estático global.
        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
            global::MercadoPago.Config.MercadoPagoConfig.AccessToken = _options.AccessToken;
    }

    public async Task<PagoResult> ProcesarPago(PagoRequest req)
    {
        // En Marketplace se cobra con el token de la ONG; si no vino, cae al token de la plataforma.
        var accessToken = !string.IsNullOrWhiteSpace(req.AccessTokenOng) ? req.AccessTokenOng : _options.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
            return new PagoResult(false, "", "Falta configurar el Access Token de MercadoPago.");
        if (string.IsNullOrWhiteSpace(req.CardToken))
            return new PagoResult(false, "", "No se recibió el token de la tarjeta.");

        var request = new PaymentCreateRequest
        {
            TransactionAmount = req.Monto,
            Token = req.CardToken,
            Description = "Donación TraceBlock",
            Installments = req.Installments <= 0 ? 1 : req.Installments,
            PaymentMethodId = req.PaymentMethodId,
            Payer = new PaymentPayerRequest { Email = req.Email }
        };

        try
        {
            var requestOptions = new RequestOptions { AccessToken = accessToken };
            var client = new PaymentClient();
            Payment payment = await client.CreateAsync(request, requestOptions);

            if (payment.Status == "approved")
                return new PagoResult(true, payment.Id?.ToString() ?? "", null);

            var detalle = payment.StatusDetail ?? payment.Status ?? "rechazado";
            return new PagoResult(false, payment.Id?.ToString() ?? "", $"Pago no aprobado: {detalle}");
        }
        catch (Exception ex)
        {
            return new PagoResult(false, "", $"Error al procesar el pago: {ex.Message}");
        }
    }
}
