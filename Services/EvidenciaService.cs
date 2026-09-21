using TrazeaBlock.Data;
using TrazeaBlock.Models;
using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services;

public class EvidenciaService
{
    private readonly AppDbContext _db;
    private readonly IBlockchainService _blockchain;
    private readonly HashingService _hasher;
    private readonly ILogger<EvidenciaService> _log;

    public EvidenciaService(AppDbContext db, IBlockchainService blockchain, HashingService hasher,
        ILogger<EvidenciaService> log)
    {
        _db = db;
        _blockchain = blockchain;
        _hasher = hasher;
        _log = log;
    }

    public async Task<Evidencia> SubirEvidenciaAsync(
        int proyectoId, string nombreArchivo, byte[] contenido,
        string descripcion, string tipo)
    {
        var hash = _hasher.CalcularSHA256(contenido);

        var evidencia = new Evidencia
        {
            ProyectoId = proyectoId,
            NombreArchivo = nombreArchivo,
            HashSHA256 = hash,
            Descripcion = descripcion,
            TipoEvidencia = tipo,
            TamanoBytes = contenido.LongLength,
            SubidoEn = DateTime.UtcNow,
            EstadoBlockchain = "PendienteBlockchain",
            RedBlockchain = _blockchain.Red
        };
        _db.Evidencias.Add(evidencia);
        // La evidencia se guarda antes de tocar la cadena; si el anclaje falla, queda pendiente
        // y AnclajePendienteService lo reintenta.
        await _db.SaveChangesAsync();

        try
        {
            evidencia.TxHashBlockchain = await _blockchain.RegistrarEvidencia(hash, proyectoId);
            evidencia.EstadoBlockchain = "RegistradoBlockchain";
        }
        catch (BlockchainPendienteException ex)
        {
            evidencia.TxHashBlockchain = ex.TxHash;
            _log.LogWarning("Evidencia {Id} enviada a {Red} pero sin confirmar aún ({Hash})", evidencia.Id, _blockchain.Red, ex.TxHash);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo anclar la evidencia {Id} en {Red}; queda pendiente", evidencia.Id, _blockchain.Red);
        }

        await _db.SaveChangesAsync();
        return evidencia;
    }
}
