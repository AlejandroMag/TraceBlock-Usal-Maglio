using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TrazeaBlock.Configuration;
using TrazeaBlock.Data;
using TrazeaBlock.Models;

namespace TrazeaBlock.Services.MercadoPago;

// Maneja el OAuth (Marketplace) de MercadoPago: URL de autorización, intercambio
// del code por tokens, persistencia por ONG y renovación (refresh) de tokens vencidos.
public class MercadoPagoConexionService
{
    private const string AuthBase = "https://auth.mercadopago.com.ar/authorization";
    private const string TokenUrl = "https://api.mercadopago.com/oauth/token";

    private readonly AppDbContext _db;
    private readonly MercadoPagoOptions _opt;
    private readonly HttpClient _http;
    private readonly ILogger<MercadoPagoConexionService> _log;

    public MercadoPagoConexionService(AppDbContext db, MercadoPagoOptions opt, HttpClient http,
        ILogger<MercadoPagoConexionService> log)
    {
        _db = db;
        _opt = opt;
        _http = http;
        _log = log;
    }

    public bool ConfigDisponible =>
        !string.IsNullOrWhiteSpace(_opt.ClientId) &&
        !string.IsNullOrWhiteSpace(_opt.ClientSecret) &&
        !string.IsNullOrWhiteSpace(_opt.RedirectUri);

    public string GetAuthorizationUrl(string state)
    {
        var redirect = Uri.EscapeDataString(_opt.RedirectUri);
        return $"{AuthBase}?client_id={_opt.ClientId}&response_type=code&platform_id=mp&redirect_uri={redirect}&state={Uri.EscapeDataString(state)}";
    }

    public async Task<OrgMercadoPago?> ObtenerConexionAsync(int orgId) =>
        await _db.OrgMercadoPagos.FirstOrDefaultAsync(m => m.OrganizacionId == orgId);

    public async Task<bool> EstaConectadaAsync(int orgId) =>
        await _db.OrgMercadoPagos.AnyAsync(m => m.OrganizacionId == orgId);

    // La cuenta de MercadoPago de una ONG queda fija desde la primera conexión. Volver a pasar
    // por el OAuth solo sirve para renovar los tokens de esa misma cuenta: así, quien obtenga
    // la contraseña de una ONG no puede desviar sus donaciones a otra cuenta. Cambiarla es una
    // acción del administrador, directo en la base (ver README).
    public async Task<ResultadoConexionMp> ConectarAsync(int orgId, string code)
    {
        var body = new
        {
            client_id = _opt.ClientId,
            client_secret = _opt.ClientSecret,
            code,
            grant_type = "authorization_code",
            redirect_uri = _opt.RedirectUri
        };

        var (tokens, error) = await PedirTokenAsync(body);
        if (tokens is null || tokens.UserId == 0 || string.IsNullOrEmpty(tokens.AccessToken))
        {
            _log.LogWarning("OAuth de MercadoPago fallido para la ONG {Org}: {Error}", orgId, error ?? "respuesta sin user_id");
            return ResultadoConexionMp.Error;
        }
        var cuenta = tokens.UserId.ToString();

        var conexion = await _db.OrgMercadoPagos.FirstOrDefaultAsync(m => m.OrganizacionId == orgId);
        if (conexion is not null && conexion.MpUserId != cuenta)
        {
            _log.LogWarning("La ONG {Org} intentó cambiar su cuenta de MercadoPago {Actual} por {Nueva}: rechazado",
                orgId, conexion.MpUserId, cuenta);
            return ResultadoConexionMp.CuentaDistinta;
        }
        if (await _db.OrgMercadoPagos.AnyAsync(m => m.MpUserId == cuenta && m.OrganizacionId != orgId))
        {
            _log.LogWarning("La ONG {Org} intentó conectar la cuenta {Cuenta}, que ya cobra para otra organización", orgId, cuenta);
            return ResultadoConexionMp.CuentaEnUso;
        }

        var nueva = conexion is null;
        if (nueva)
        {
            conexion = new OrgMercadoPago { OrganizacionId = orgId, MpUserId = cuenta, FechaConexion = DateTime.UtcNow };
            _db.OrgMercadoPagos.Add(conexion);
        }
        conexion!.AccessToken = tokens.AccessToken ?? "";
        conexion.RefreshToken = tokens.RefreshToken ?? "";
        conexion.PublicKey = tokens.PublicKey ?? "";
        conexion.ExpiraEn = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn > 0 ? tokens.ExpiresIn : 21600);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // El índice único de MpUserId frena a dos ONGs conectando la misma cuenta a la vez.
            _log.LogWarning(ex, "La cuenta {Cuenta} no se pudo asociar a la ONG {Org}", cuenta, orgId);
            return ResultadoConexionMp.CuentaEnUso;
        }
        return nueva ? ResultadoConexionMp.Conectada : ResultadoConexionMp.Renovada;
    }

    // Devuelve un access token válido para la ONG, refrescándolo si venció.
    public async Task<(string? accessToken, string? publicKey)> ObtenerCredencialesValidasAsync(int orgId)
    {
        var conexion = await _db.OrgMercadoPagos.FirstOrDefaultAsync(m => m.OrganizacionId == orgId);
        if (conexion is null) return (null, null);

        if (conexion.ExpiraEn <= DateTime.UtcNow.AddMinutes(5) && !string.IsNullOrEmpty(conexion.RefreshToken))
        {
            var body = new
            {
                client_id = _opt.ClientId,
                client_secret = _opt.ClientSecret,
                grant_type = "refresh_token",
                refresh_token = conexion.RefreshToken
            };
            var (tokens, _) = await PedirTokenAsync(body);
            if (tokens is not null)
            {
                conexion.AccessToken = tokens.AccessToken ?? conexion.AccessToken;
                if (!string.IsNullOrEmpty(tokens.RefreshToken)) conexion.RefreshToken = tokens.RefreshToken;
                if (!string.IsNullOrEmpty(tokens.PublicKey)) conexion.PublicKey = tokens.PublicKey;
                conexion.ExpiraEn = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn > 0 ? tokens.ExpiresIn : 21600);
                await _db.SaveChangesAsync();
            }
        }

        return (conexion.AccessToken, conexion.PublicKey);
    }

    // Crea una preferencia de Checkout Pro con el token de la ONG (la plata va a su cuenta).
    public async Task<(string? initPoint, string? error)> CrearPreferenciaAsync(
        int orgId, string titulo, decimal monto, string externalRef, string backUrl)
    {
        var (accessToken, _) = await ObtenerCredencialesValidasAsync(orgId);
        if (string.IsNullOrWhiteSpace(accessToken))
            return (null, "La organización no tiene MercadoPago conectado.");

        var body = new
        {
            items = new[] { new { title = titulo, quantity = 1, unit_price = monto, currency_id = "ARS" } },
            external_reference = externalRef,
            back_urls = new { success = backUrl, pending = backUrl, failure = backUrl },
            auto_return = "approved"
        };

        try
        {
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/checkout/preferences") { Content = content };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _http.SendAsync(req);
            var texto = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return (null, $"MP preferencia {(int)resp.StatusCode}: {texto}");
            using var doc = JsonDocument.Parse(texto);
            return (doc.RootElement.GetProperty("init_point").GetString(), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    // Verifica el estado de un pago consultando con el token de la ONG.
    // Consulta el pago a MercadoPago con el token de la ONG. Devuelve los datos crudos:
    // decidir si el pago corresponde a una donación es responsabilidad de quien llama.
    public async Task<(PagoMercadoPago? pago, string? error)> ObtenerPagoAsync(int orgId, string paymentId)
    {
        var (accessToken, _) = await ObtenerCredencialesValidasAsync(orgId);
        if (string.IsNullOrWhiteSpace(accessToken)) return (null, "Sin conexión.");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.mercadopago.com/v1/payments/{Uri.EscapeDataString(paymentId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _http.SendAsync(req);
            var texto = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return (null, $"MP pago {(int)resp.StatusCode}: {texto}");

            using var doc = JsonDocument.Parse(texto);
            var r = doc.RootElement;
            string? Texto(string prop) => r.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null
                ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
                : null;

            return (new PagoMercadoPago(
                Id: Texto("id") ?? paymentId,
                Status: Texto("status") ?? "",
                Monto: r.GetProperty("transaction_amount").GetDecimal(),
                Moneda: Texto("currency_id"),
                ExternalReference: Texto("external_reference"),
                CollectorId: Texto("collector_id")), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private async Task<(TokenResponse? tokens, string? error)> PedirTokenAsync(object body)
    {
        try
        {
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(TokenUrl, content);
            var texto = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return (null, $"MercadoPago OAuth {(int)resp.StatusCode}: {texto}");

            var tokens = JsonSerializer.Deserialize<TokenResponse>(texto);
            return (tokens, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("public_key")] public string? PublicKey { get; set; }
        [JsonPropertyName("user_id")] public long UserId { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }
}

public enum ResultadoConexionMp
{
    Conectada,       // primera conexión: la cuenta queda asociada a la ONG
    Renovada,        // misma cuenta de siempre, tokens nuevos
    CuentaDistinta,  // la ONG ya tiene otra cuenta: solo la cambia el administrador
    CuentaEnUso,     // esa cuenta ya cobra para otra ONG
    Error
}

// Lo que MercadoPago dice de un pago. CollectorId es la cuenta que cobró.
public record PagoMercadoPago(
    string Id, string Status, decimal Monto, string? Moneda, string? ExternalReference, string? CollectorId);
