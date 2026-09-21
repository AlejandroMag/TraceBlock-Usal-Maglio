namespace TrazeaBlock.Configuration;

// Conexión con Polygon. Lo público va en appsettings; la clave privada de la billetera de la
// plataforma y (si se usa) la URL del RPC con API key van por user-secrets / variables de entorno.
public class BlockchainOptions
{
    /// <summary>false = servicio simulado (sin billetera ni RPC). true = Polygon real.</summary>
    public bool UsarBlockchainReal { get; set; }

    /// <summary>Nombre corto de la red, se guarda junto a cada hash (ej. "amoy").</summary>
    public string Red { get; set; } = "amoy";

    public long ChainId { get; set; } = 80002;

    /// <summary>
    /// Endpoint JSON-RPC. Un RPC público sirve para empezar (el oficial rpc-amoy.polygon.technology
    /// ya no existe); para la defensa conviene uno propio (Alchemy) cargado por user-secrets.
    /// </summary>
    public string RpcUrl { get; set; } = "https://polygon-amoy-bor-rpc.publicnode.com";

    /// <summary>Dirección del contrato TrazeaBlockRegistry desplegado.</summary>
    public string ContractAddress { get; set; } = "";

    /// <summary>Clave privada de la billetera de la plataforma. SECRETO: nunca en appsettings.</summary>
    public string PrivateKey { get; set; } = "";

    public string ExplorerBaseUrl { get; set; } = "https://amoy.polygonscan.com";

    /// <summary>Cuánto esperar la confirmación antes de dejar el registro como pendiente.</summary>
    public int TimeoutSegundos { get; set; } = 45;

    /// <summary>Piso opcional de gas price en gwei, por si la red rechaza transacciones "underpriced".</summary>
    public decimal? GasPriceMinGwei { get; set; }

    public string UrlTx(string txHash) => $"{ExplorerBaseUrl.TrimEnd('/')}/tx/{txHash}";
    public string UrlContrato() => $"{ExplorerBaseUrl.TrimEnd('/')}/address/{ContractAddress}";
}
