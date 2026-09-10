using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.Mock;

public class MockPagoService : IPagoService
{
    public async Task<PagoResult> ProcesarPago(PagoRequest req)
    {
        await Task.Delay(1000);
        var paymentId = "MP-" + Random.Shared.Next(100000, 999999);
        return new PagoResult(true, paymentId, null);
    }
}
