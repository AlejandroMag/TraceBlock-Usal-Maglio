using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.Mock;

public class MockAfipService : IAfipService
{
    public async Task<AfipResult> ValidarCUIT(string cuit)
    {
        await Task.Delay(800);
        return new AfipResult(true, "Organización Demo SA", null);
    }
}
