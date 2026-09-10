namespace TrazeaBlock.Services.Interfaces;

public interface IBlockchainService
{
    Task<string> RegistrarDonacion(string tokenId, decimal monto, int proyectoId);
    Task<string> RegistrarEvidencia(string hashArchivo, int proyectoId);
    Task<BlockchainVerification> VerificarTransaccion(string txHash);
}

public record BlockchainVerification(bool Valido, int Confirmaciones, string? Error);
