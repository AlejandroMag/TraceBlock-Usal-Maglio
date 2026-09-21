using Microsoft.EntityFrameworkCore;
using TrazeaBlock.Configuration;
using TrazeaBlock.Data;
using TrazeaBlock.Models;
using TrazeaBlock.Services.Interfaces;
using TrazeaBlock.Services.MercadoPago;

namespace TrazeaBlock.Services;

public class DonacionService
{
    private readonly AppDbContext _db;
    private readonly IBlockchainService _blockchain;
    private readonly IPagoService _pago;
    private readonly MercadoPagoConexionService _conexion;
    private readonly ILogger<DonacionService> _log;

    public DonacionService(AppDbContext db, IBlockchainService blockchain, IPagoService pago,
        MercadoPagoConexionService conexion, ILogger<DonacionService> log)
    {
        _db = db;
        _blockchain = blockchain;
        _pago = pago;
        _conexion = conexion;
        _log = log;
    }

    public async Task<List<Donacion>> ObtenerDonacionesUsuarioAsync(int usuarioId)
    {
        return await _db.Donaciones
            .Where(d => d.UsuarioId == usuarioId)
            .Include(d => d.Proyecto).ThenInclude(p => p.Organizacion)
            .Include(d => d.Proyecto).ThenInclude(p => p.Hitos)
            .Include(d => d.Proyecto).ThenInclude(p => p.Evidencias)
            .Include(d => d.Proyecto).ThenInclude(p => p.Comunicados)
            .Include(d => d.Proyecto).ThenInclude(p => p.Donaciones).ThenInclude(x => x.Usuario)
            .OrderByDescending(d => d.FechaDonacion)
            .ToListAsync();
    }

    // --- Checkout Pro (marketplace): crea la donación pendiente + la preferencia, devuelve la URL de MP ---
    public async Task<(string? initPoint, string? error)> IniciarDonacionCheckoutProAsync(
        int usuarioId, int proyectoId, decimal monto, string? mensaje, bool anonima, string backUrlBase)
    {
        if (monto < 100) return (null, "El monto mínimo de donación es $100.");

        var proyecto = await _db.Proyectos.Include(p => p.Organizacion)
            .FirstOrDefaultAsync(p => p.Id == proyectoId);
        if (proyecto is null) return (null, "El proyecto no existe.");

        if (!await _conexion.EstaConectadaAsync(proyecto.OrganizacionId))
            return (null, "La organización todavía no configuró sus cobros con MercadoPago.");

        var tokenId = Guid.NewGuid().ToString("N").ToUpper();
        var pend = new DonacionPendiente
        {
            TokenId = tokenId,
            UsuarioId = usuarioId,
            ProyectoId = proyectoId,
            Monto = monto,
            Mensaje = mensaje,
            Anonima = anonima
        };
        _db.DonacionesPendientes.Add(pend);
        await _db.SaveChangesAsync();

        var backUrl = backUrlBase.TrimEnd('/') + "/donar/retorno";
        var (initPoint, error) = await _conexion.CrearPreferenciaAsync(
            proyecto.OrganizacionId, "Donación: " + proyecto.Titulo, monto, tokenId, backUrl);

        if (initPoint is null)
        {
            _db.DonacionesPendientes.Remove(pend);
            await _db.SaveChangesAsync();
            return (null, error ?? "No se pudo iniciar el pago.");
        }
        return (initPoint, null);
    }

    // Al volver de MP: verifica el pago y convierte la pendiente en Donacion real.
    public async Task<(Donacion? donacion, string? error)> FinalizarDonacionAsync(string tokenId, string? paymentId)
    {
        var existente = await _db.Donaciones.FirstOrDefaultAsync(d => d.TokenId == tokenId);
        if (existente is not null) return (existente, null); // idempotente

        var pend = await _db.DonacionesPendientes.FirstOrDefaultAsync(p => p.TokenId == tokenId);
        if (pend is null) return (null, "No se encontró la donación pendiente.");

        var proyecto = await _db.Proyectos.FindAsync(pend.ProyectoId);
        if (proyecto is null) return (null, "El proyecto no existe.");

        if (string.IsNullOrWhiteSpace(paymentId))
            return (null, "El pago no llegó confirmado.");

        var (approved, _, _) = await _conexion.VerificarPagoAsync(proyecto.OrganizacionId, paymentId);
        if (!approved) return (null, "El pago no fue aprobado.");

        var donacion = new Donacion
        {
            ProyectoId = pend.ProyectoId,
            UsuarioId = pend.UsuarioId,
            TokenId = tokenId,
            Monto = pend.Monto,
            Mensaje = pend.Mensaje,
            Anonima = pend.Anonima,
            FechaDonacion = DateTime.UtcNow,
            Estado = "PendienteBlockchain",
            PaymentId = paymentId
        };
        _db.Donaciones.Add(donacion);
        proyecto.MontoRecaudado += pend.Monto;
        _db.DonacionesPendientes.Remove(pend);

        // El pago ya se hizo: la donación se persiste antes de tocar la cadena.
        await _db.SaveChangesAsync();

        await AnclarAsync(donacion);
        await _db.SaveChangesAsync();
        return (donacion, null);
    }

    public async Task<Donacion?> RastrearPorTokenAsync(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId)) return null;
        var normalizado = tokenId.ToUpper().Replace("-", "").Trim();

        return await _db.Donaciones
            .Include(d => d.Usuario)
            .Include(d => d.Proyecto).ThenInclude(p => p.Organizacion)
            .Include(d => d.Proyecto).ThenInclude(p => p.Hitos)
            .Include(d => d.Proyecto).ThenInclude(p => p.Evidencias)
            .FirstOrDefaultAsync(d => d.TokenId == normalizado);
    }

    // --- Alta sin pasarela: solo cuando ValidarPagoMercadoPago está en false. El flujo
    //     que se usa en producción y en las pruebas es IniciarDonacionCheckoutProAsync. ---
    public async Task<(Donacion? donacion, string? error)> CrearDonacionAsync(
        int usuarioId, int proyectoId, decimal monto, string? mensaje, bool anonima)
    {
        if (monto < 100)
            return (null, "El monto mínimo de donación es $100.");

        var proyecto = await _db.Proyectos
            .Include(p => p.Organizacion)
            .FirstOrDefaultAsync(p => p.Id == proyectoId);
        if (proyecto is null)
            return (null, "El proyecto no existe.");

        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
            return (null, "Usuario no encontrado.");

        var tokenId = Guid.NewGuid().ToString("N").ToUpper();

        var pago = await _pago.ProcesarPago(new PagoRequest(monto, usuario.Email, tokenId));
        if (!pago.Exitoso)
            return (null, pago.Error ?? "No se pudo procesar el pago.");

        var donacion = new Donacion
        {
            ProyectoId = proyectoId,
            UsuarioId = usuarioId,
            TokenId = tokenId,
            Monto = monto,
            Mensaje = mensaje,
            Anonima = anonima,
            FechaDonacion = DateTime.UtcNow,
            Estado = "PendienteBlockchain",
            PaymentId = pago.PaymentId
        };
        _db.Donaciones.Add(donacion);
        proyecto.MontoRecaudado += monto;
        await _db.SaveChangesAsync();

        await AnclarAsync(donacion);
        await _db.SaveChangesAsync();
        return (donacion, null);
    }

    // Guarda primero, ancla después: si la cadena falla o tarda, la donación queda
    // "PendienteBlockchain" y AnclajePendienteService la retoma. Nunca se pierde el pago.
    private async Task AnclarAsync(Donacion donacion)
    {
        donacion.RedBlockchain = _blockchain.Red;
        try
        {
            donacion.TxHashBlockchain = await _blockchain.RegistrarDonacion(donacion.TokenId, donacion.Monto, donacion.ProyectoId);
            donacion.Estado = "RegistradoBlockchain";
        }
        catch (BlockchainPendienteException ex)
        {
            donacion.TxHashBlockchain = ex.TxHash;   // enviada; se confirma después
            donacion.Estado = "PendienteBlockchain";
            _log.LogWarning("Donación {Token} enviada a {Red} pero sin confirmar aún ({Hash})", donacion.TokenId, _blockchain.Red, ex.TxHash);
        }
        catch (Exception ex)
        {
            donacion.TxHashBlockchain = "";
            donacion.Estado = "PendienteBlockchain";
            _log.LogError(ex, "No se pudo anclar la donación {Token} en {Red}; queda pendiente", donacion.TokenId, _blockchain.Red);
        }
    }
}
