namespace TrazeaBlock.Services.Interfaces;

public interface IBlockchainService
{
    /// <summary>Nombre de la red en la que se registran los hashes ("mock" o, por ejemplo, "amoy").</summary>
    string Red { get; }

    /// <summary>
    /// Registra la donación y devuelve el hash de la transacción confirmada.
    /// Lanza <see cref="BlockchainPendienteException"/> si se envió pero no se confirmó a tiempo.
    /// </summary>
    Task<string> RegistrarDonacion(string tokenId, decimal monto, int proyectoId);

    Task<string> RegistrarEvidencia(string hashArchivo, int proyectoId);

    /// <summary>¿La transacción existe, fue exitosa y cuántas confirmaciones tiene?</summary>
    Task<BlockchainVerification> VerificarTransaccion(string txHash);

    /// <summary>Además de la transacción, comprueba que lo registrado en cadena coincida con estos datos.</summary>
    Task<BlockchainVerification> VerificarDonacion(string txHash, string tokenId, decimal monto, int proyectoId);

    Task<BlockchainVerification> VerificarEvidencia(string txHash, string hashArchivo, int proyectoId);
}

/// <param name="Valido">La transacción existe y fue exitosa.</param>
/// <param name="IntegridadOk">Lo registrado en cadena coincide con los datos de TraceBlock.</param>
/// <param name="Revertida">La transacción fue minada pero falló: hay que reenviarla.</param>
public record BlockchainVerification(
    bool Valido,
    int Confirmaciones,
    string? Error,
    bool IntegridadOk = true,
    string? Detalle = null,
    bool Revertida = false);

/// <summary>
/// La transacción se envió (tiene hash) pero no se confirmó dentro del tiempo de espera.
/// El registro queda pendiente y el servicio de fondo vuelve a consultar su estado.
/// </summary>
public class BlockchainPendienteException : Exception
{
    public string TxHash { get; }

    public BlockchainPendienteException(string txHash, string mensaje) : base(mensaje)
    {
        TxHash = txHash;
    }
}
