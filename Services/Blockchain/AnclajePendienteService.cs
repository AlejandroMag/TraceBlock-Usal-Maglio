using Microsoft.EntityFrameworkCore;
using TrazeaBlock.Data;
using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.Blockchain;

// Cuando llega un pago confirmado, el dinero ya se movió: la donación se guarda primero y el
// anclaje en la cadena se intenta después. Si el RPC está caído o la transacción tarda, el
// registro queda "PendienteBlockchain" y este servicio lo retoma cada un minuto.
public class AnclajePendienteService : BackgroundService
{
    private const string Pendiente = "PendienteBlockchain";
    private const string Registrado = "RegistradoBlockchain";
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AnclajePendienteService> _log;

    public AnclajePendienteService(IServiceScopeFactory scopes, ILogger<AnclajePendienteService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(EsperaInicial, ct); } catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcesarAsync(ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error reintentando anclajes pendientes");
            }

            try { await Task.Delay(Intervalo, ct); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task ProcesarAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cadena = scope.ServiceProvider.GetRequiredService<IBlockchainService>();

        var donaciones = await db.Donaciones.Where(d => d.Estado == Pendiente).Take(20).ToListAsync(ct);
        foreach (var d in donaciones)
        {
            var resultado = await ResolverAsync(
                d.TxHashBlockchain,
                () => cadena.RegistrarDonacion(d.TokenId, d.Monto, d.ProyectoId),
                cadena, $"donación {d.TokenId}");
            if (resultado is null) continue;
            (d.TxHashBlockchain, var confirmado) = resultado.Value;
            d.RedBlockchain = cadena.Red;
            if (confirmado) d.Estado = Registrado;
        }

        var evidencias = await db.Evidencias.Where(e => e.EstadoBlockchain == Pendiente).Take(20).ToListAsync(ct);
        foreach (var e in evidencias)
        {
            var resultado = await ResolverAsync(
                e.TxHashBlockchain,
                () => cadena.RegistrarEvidencia(e.HashSHA256, e.ProyectoId),
                cadena, $"evidencia {e.Id}");
            if (resultado is null) continue;
            (e.TxHashBlockchain, var confirmado) = resultado.Value;
            e.RedBlockchain = cadena.Red;
            if (confirmado) e.EstadoBlockchain = Registrado;
        }

        if (donaciones.Count > 0 || evidencias.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    // Devuelve (hash, confirmado) con el nuevo estado del registro, o null si no hay cambios.
    // Si ya había un hash, primero consulta si esa transacción se confirmó: reenviar a ciegas
    // duplicaría el registro en la cadena.
    private async Task<(string hash, bool confirmado)?> ResolverAsync(
        string hashActual, Func<Task<string>> registrar, IBlockchainService cadena, string descripcion)
    {
        if (!string.IsNullOrEmpty(hashActual))
        {
            var v = await cadena.VerificarTransaccion(hashActual);
            if (v.Valido)
            {
                _log.LogInformation("Anclaje pendiente confirmado: {Desc} -> {Hash}", descripcion, hashActual);
                return (hashActual, true);
            }
            if (!v.Revertida) return null;   // todavía no está minada: esperar la próxima vuelta
            _log.LogWarning("Transacción revertida, se reenvía: {Desc} ({Hash})", descripcion, hashActual);
        }

        try
        {
            var hash = await registrar();
            _log.LogInformation("Anclaje pendiente resuelto: {Desc} -> {Hash}", descripcion, hash);
            return (hash, true);
        }
        catch (BlockchainPendienteException ex)
        {
            return (ex.TxHash, false);       // enviada; se confirma en una próxima vuelta
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo anclar {Desc}; se reintenta más tarde", descripcion);
            return null;
        }
    }
}
