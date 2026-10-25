using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.Mock;

public class MockPagoService : IPagoService
{
    public async Task<PagoResult> ProcesarPago(PagoRequest req)
    {
        await Task.Delay(1000);
        // Único (el índice de PaymentId no admite repetidos) y distinguible de un ID real de MP.
        var paymentId = "SIM-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
        return new PagoResult(true, paymentId, null);
    }
}
