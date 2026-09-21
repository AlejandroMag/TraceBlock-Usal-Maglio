using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using TrazeaBlock.Configuration;
using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.Blockchain;

// Registro real en Polygon a través del contrato TrazeaBlockRegistry.
// La plataforma tiene una única billetera que firma todas las transacciones: ni las ONGs
// ni los donantes necesitan wallet. A la cadena solo van identificadores anónimos, montos
// y hashes; nunca datos personales.
public class PolygonBlockchainService : IBlockchainService
{
    private readonly BlockchainOptions _opt;
    private readonly ILogger<PolygonBlockchainService> _log;
    private readonly Web3 _web3;
    private readonly ContractHandler _contrato;

    // Una sola billetera firma todo: los envíos se serializan para que dos transacciones
    // simultáneas no tomen el mismo nonce (número de secuencia) y una quede rechazada.
    private readonly SemaphoreSlim _envio = new(1, 1);

    private static readonly TimeSpan IntervaloSondeo = TimeSpan.FromSeconds(2);

    public string Red => _opt.Red;
    public string DireccionPlataforma { get; }

    public PolygonBlockchainService(BlockchainOptions opt, ILogger<PolygonBlockchainService> log)
    {
        _opt = opt;
        _log = log;

        if (string.IsNullOrWhiteSpace(opt.PrivateKey))
            throw new InvalidOperationException(
                "Blockchain:UsarBlockchainReal está activo pero falta Blockchain:PrivateKey. Cargala con user-secrets.");
        if (string.IsNullOrWhiteSpace(opt.ContractAddress))
            throw new InvalidOperationException(
                "Blockchain:UsarBlockchainReal está activo pero falta Blockchain:ContractAddress en appsettings.");

        var cuenta = new Account(opt.PrivateKey, opt.ChainId);
        DireccionPlataforma = cuenta.Address;
        _web3 = new Web3(cuenta, opt.RpcUrl);
        _contrato = _web3.Eth.GetContractHandler(opt.ContractAddress);

        _log.LogInformation("Blockchain real: red {Red}, contrato {Contrato}, plataforma {Cuenta}",
            opt.Red, opt.ContractAddress, DireccionPlataforma);
    }

    // ---------- Registro ----------

    public Task<string> RegistrarDonacion(string tokenId, decimal monto, int proyectoId)
    {
        var fn = new RegistrarDonacionFunction
        {
            TokenId = TokenIdABytes32(tokenId),
            MontoCentavos = ACentavos(monto),
            ProyectoId = proyectoId
        };
        return EnviarYEsperarAsync(fn, $"donación {tokenId}");
    }

    public Task<string> RegistrarEvidencia(string hashArchivo, int proyectoId)
    {
        var fn = new RegistrarEvidenciaFunction
        {
            HashArchivo = HexABytes32(hashArchivo),
            ProyectoId = proyectoId
        };
        return EnviarYEsperarAsync(fn, $"evidencia {hashArchivo[..Math.Min(12, hashArchivo.Length)]}…");
    }

    private async Task<string> EnviarYEsperarAsync<TFuncion>(TFuncion fn, string descripcion)
        where TFuncion : FunctionMessage, new()
    {
        AplicarPisoDeGas(fn);

        string hash;
        await _envio.WaitAsync();
        try
        {
            // Devuelve el hash apenas el nodo acepta la transacción; la confirmación viene después.
            hash = await _contrato.SendRequestAsync(fn);
        }
        finally
        {
            _envio.Release();
        }
        _log.LogInformation("Enviada a {Red} la {Desc}: {Hash}", Red, descripcion, hash);

        var recibo = await EsperarReciboAsync(hash, TimeSpan.FromSeconds(_opt.TimeoutSegundos));
        if (recibo is null)
            throw new BlockchainPendienteException(hash,
                $"La transacción {hash} se envió pero no se confirmó en {_opt.TimeoutSegundos}s.");
        if (!EsExitosa(recibo))
            throw new InvalidOperationException($"La transacción {hash} fue revertida por la red.");

        _log.LogInformation("Confirmada en bloque {Bloque}: {Hash}", recibo.BlockNumber.Value, hash);
        return hash;
    }

    private async Task<TransactionReceipt?> EsperarReciboAsync(string hash, TimeSpan timeout)
    {
        var limite = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < limite)
        {
            var recibo = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(hash);
            if (recibo is not null) return recibo;
            await Task.Delay(IntervaloSondeo);
        }
        return null;
    }

    private void AplicarPisoDeGas(FunctionMessage fn)
    {
        // Solo si la red rechaza transacciones "underpriced": fuerza un precio mínimo (tx legacy).
        if (_opt.GasPriceMinGwei is { } gwei && gwei > 0)
            fn.GasPrice = Web3.Convert.ToWei(gwei, UnitConversion.EthUnit.Gwei);
    }

    // ---------- Verificación ----------

    public async Task<BlockchainVerification> VerificarTransaccion(string txHash)
    {
        var (verificacion, _) = await VerificarBaseAsync(txHash);
        return verificacion;
    }

    public async Task<BlockchainVerification> VerificarDonacion(string txHash, string tokenId, decimal monto, int proyectoId)
    {
        var (baseVerif, recibo) = await VerificarBaseAsync(txHash);
        if (!baseVerif.Valido || recibo is null) return baseVerif;

        var evento = recibo.DecodeAllEvents<DonacionRegistradaEventDTO>().FirstOrDefault()?.Event;
        if (evento is null)
            return baseVerif with { IntegridadOk = false, Detalle = "La transacción no contiene un evento DonacionRegistrada." };

        var diferencias = new List<string>();
        if (!evento.TokenId.AsSpan().SequenceEqual(TokenIdABytes32(tokenId))) diferencias.Add("token ID");
        if (evento.MontoCentavos != ACentavos(monto)) diferencias.Add("monto");
        if (evento.ProyectoId != proyectoId) diferencias.Add("proyecto");

        return diferencias.Count == 0
            ? baseVerif with { IntegridadOk = true, Detalle = $"La cadena coincide con TraceBlock (bloque {recibo.BlockNumber.Value})." }
            : baseVerif with { IntegridadOk = false, Detalle = "No coincide: " + string.Join(", ", diferencias) + "." };
    }

    public async Task<BlockchainVerification> VerificarEvidencia(string txHash, string hashArchivo, int proyectoId)
    {
        var (baseVerif, recibo) = await VerificarBaseAsync(txHash);
        if (!baseVerif.Valido || recibo is null) return baseVerif;

        var evento = recibo.DecodeAllEvents<EvidenciaRegistradaEventDTO>().FirstOrDefault()?.Event;
        if (evento is null)
            return baseVerif with { IntegridadOk = false, Detalle = "La transacción no contiene un evento EvidenciaRegistrada." };

        var diferencias = new List<string>();
        if (!evento.HashArchivo.AsSpan().SequenceEqual(HexABytes32(hashArchivo))) diferencias.Add("huella SHA-256");
        if (evento.ProyectoId != proyectoId) diferencias.Add("proyecto");

        return diferencias.Count == 0
            ? baseVerif with { IntegridadOk = true, Detalle = $"La cadena coincide con TraceBlock (bloque {recibo.BlockNumber.Value})." }
            : baseVerif with { IntegridadOk = false, Detalle = "No coincide: " + string.Join(", ", diferencias) + "." };
    }

    // Existencia, éxito y confirmaciones. Devuelve también el recibo para decodificar eventos.
    private async Task<(BlockchainVerification, TransactionReceipt?)> VerificarBaseAsync(string txHash)
    {
        try
        {
            var recibo = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            if (recibo is null)
                return (new BlockchainVerification(false, 0, "La transacción todavía no fue minada, o no existe en esta red."), null);
            if (!EsExitosa(recibo))
                return (new BlockchainVerification(false, 0, "La transacción fue revertida por la red.", Revertida: true), recibo);
            if (!string.Equals(recibo.To, _opt.ContractAddress, StringComparison.OrdinalIgnoreCase))
                return (new BlockchainVerification(false, 0, "La transacción no apunta al contrato de TraceBlock."), recibo);

            var ultimo = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var confirmaciones = (int)BigInteger.Min(ultimo.Value - recibo.BlockNumber.Value + 1, int.MaxValue);
            return (new BlockchainVerification(true, confirmaciones, null, true, $"Bloque {recibo.BlockNumber.Value}"), recibo);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo verificar {Hash} en {Red}", txHash, Red);
            return (new BlockchainVerification(false, 0, "No se pudo consultar la red: " + ex.Message), null);
        }
    }

    private static bool EsExitosa(TransactionReceipt recibo) => recibo.Status?.Value == 1;

    // ---------- Conversiones ----------

    // El token ID de TraceBlock es un GUID de 32 hex (16 bytes). Se guarda en un bytes32
    // rellenado con ceros a la izquierda, así en el explorador se lee directamente.
    public static byte[]TokenIdABytes32(string tokenId)
    {
        var bytes = Convert.FromHexString(tokenId);
        if (bytes.Length > 32) throw new ArgumentException("El token ID excede 32 bytes.", nameof(tokenId));
        var resultado = new byte[32];
        Array.Copy(bytes, 0, resultado, 32 - bytes.Length, bytes.Length);
        return resultado;
    }

    // Un SHA-256 en hex (64 caracteres) entra exacto en un bytes32.
    public static byte[]HexABytes32(string hex)
    {
        var limpio = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
        var bytes = Convert.FromHexString(limpio);
        if (bytes.Length != 32) throw new ArgumentException("Se esperaba un hash de 32 bytes.", nameof(hex));
        return bytes;
    }

    // Los montos van en centavos para no tener decimales en la cadena.
    public static BigInteger ACentavos(decimal monto) =>
        new(decimal.Round(monto * 100m, 0, MidpointRounding.AwayFromZero));
}
