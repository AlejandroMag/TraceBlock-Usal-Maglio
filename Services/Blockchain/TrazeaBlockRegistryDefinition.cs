using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace TrazeaBlock.Services.Blockchain;

// Espejo en C# de la interfaz del contrato Contracts/TrazeaBlockRegistry.sol.
// Nethereum usa estos atributos para codificar las llamadas y decodificar los eventos.
// Si cambia el contrato, hay que actualizar estas clases.

[Function("registrarDonacion")]
public class RegistrarDonacionFunction : FunctionMessage
{
    [Parameter("bytes32", "tokenId", 1)] public byte[] TokenId { get; set; } = Array.Empty<byte>();
    [Parameter("uint256", "montoCentavos", 2)] public BigInteger MontoCentavos { get; set; }
    [Parameter("uint256", "proyectoId", 3)] public BigInteger ProyectoId { get; set; }
}

[Function("registrarEvidencia")]
public class RegistrarEvidenciaFunction : FunctionMessage
{
    [Parameter("bytes32", "hashArchivo", 1)] public byte[] HashArchivo { get; set; } = Array.Empty<byte>();
    [Parameter("uint256", "proyectoId", 2)] public BigInteger ProyectoId { get; set; }
}

[Event("DonacionRegistrada")]
public class DonacionRegistradaEventDTO : IEventDTO
{
    [Parameter("bytes32", "tokenId", 1, true)] public byte[] TokenId { get; set; } = Array.Empty<byte>();
    [Parameter("uint256", "montoCentavos", 2, false)] public BigInteger MontoCentavos { get; set; }
    [Parameter("uint256", "proyectoId", 3, true)] public BigInteger ProyectoId { get; set; }
    [Parameter("uint256", "timestamp", 4, false)] public BigInteger Timestamp { get; set; }
}

[Event("EvidenciaRegistrada")]
public class EvidenciaRegistradaEventDTO : IEventDTO
{
    [Parameter("bytes32", "hashArchivo", 1, true)] public byte[] HashArchivo { get; set; } = Array.Empty<byte>();
    [Parameter("uint256", "proyectoId", 2, true)] public BigInteger ProyectoId { get; set; }
    [Parameter("uint256", "timestamp", 3, false)] public BigInteger Timestamp { get; set; }
}
