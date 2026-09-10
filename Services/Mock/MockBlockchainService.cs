using System.Security.Cryptography;
using System.Text;
using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.Mock;

public class MockBlockchainService : IBlockchainService
{
    public async Task<string> RegistrarDonacion(string tokenId, decimal monto, int proyectoId)
    {
        await Task.Delay(1200);
        var seed = tokenId + monto + proyectoId + DateTime.UtcNow.Ticks;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return "0x" + Convert.ToHexString(hash).ToLower();
    }

    public async Task<string> RegistrarEvidencia(string hashArchivo, int proyectoId)
    {
        await Task.Delay(900);
        var seed = hashArchivo + proyectoId + DateTime.UtcNow.Ticks;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return "0x" + Convert.ToHexString(hash).ToLower();
    }

    public async Task<BlockchainVerification> VerificarTransaccion(string txHash)
    {
        await Task.Delay(600);
        return new BlockchainVerification(true, 14, null);
    }
}
