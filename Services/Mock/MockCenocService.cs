using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.Mock;

public class MockCenocService : ICenocService
{
    public async Task<CenocResult> ValidarOrganizacion(string cuit, byte[] pdfBytes)
    {
        await Task.Delay(1000);
        return new CenocResult(true, "Org registrada ante CENOC", null);
    }
}
