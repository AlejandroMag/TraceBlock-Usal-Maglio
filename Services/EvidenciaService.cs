using TrazeaBlock.Data;
using TrazeaBlock.Models;
using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services;

public class EvidenciaService
{
    private readonly AppDbContext _db;
    private readonly IBlockchainService _blockchain;
    private readonly HashingService _hasher;

    public EvidenciaService(AppDbContext db, IBlockchainService blockchain, HashingService hasher)
    {
        _db = db;
        _blockchain = blockchain;
        _hasher = hasher;
    }

    public async Task<Evidencia> SubirEvidenciaAsync(
        int proyectoId, string nombreArchivo, byte[] contenido,
        string descripcion, string tipo)
    {
        var hash = _hasher.CalcularSHA256(contenido);
        var txHash = await _blockchain.RegistrarEvidencia(hash, proyectoId);

        var evidencia = new Evidencia
        {
            ProyectoId = proyectoId,
            NombreArchivo = nombreArchivo,
            HashSHA256 = hash,
            TxHashBlockchain = txHash,
            Descripcion = descripcion,
            TipoEvidencia = tipo,
            TamanoBytes = contenido.LongLength,
            SubidoEn = DateTime.UtcNow
        };
        _db.Evidencias.Add(evidencia);
        await _db.SaveChangesAsync();
        return evidencia;
    }
}
