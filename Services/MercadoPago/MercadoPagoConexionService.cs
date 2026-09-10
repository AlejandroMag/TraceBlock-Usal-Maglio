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

    public MercadoPagoConexionService(AppDbContext db, MercadoPagoOptions opt, HttpClient http)
    {
        _db = db;
        _opt = opt;
        _http = http;
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

    public async Task<(bool ok, string? error)> ConectarAsync(int orgId, string code)
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
        if (tokens is null) return (false, error);

        var conexion = await _db.OrgMercadoPagos.FirstOrDefaultAsync(m => m.OrganizacionId == orgId);
        if (conexion is null)
        {
            conexion = new OrgMercadoPago { OrganizacionId = orgId };
            _db.OrgMercadoPagos.Add(conexion);
        }
        conexion.MpUserId = tokens.UserId.ToString();
        conexion.AccessToken = tokens.AccessToken ?? "";
        conexion.RefreshToken = tokens.RefreshToken ?? "";
        conexion.PublicKey = tokens.PublicKey ?? "";
        conexion.ExpiraEn = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn > 0 ? tokens.ExpiresIn : 21600);
        conexion.FechaConexion = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (true, null);
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
    public async Task<(bool approved, decimal monto, string? error)> VerificarPagoAsync(int orgId, string paymentId)
    {
        var (accessToken, _) = await ObtenerCredencialesValidasAsync(orgId);
        if (string.IsNullOrWhiteSpace(accessToken)) return (false, 0, "Sin conexión.");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/v1/payments/{paymentId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _http.SendAsync(req);
            var texto = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return (false, 0, texto);
            using var doc = JsonDocument.Parse(texto);
            var status = doc.RootElement.GetProperty("status").GetString();
            var monto = doc.RootElement.GetProperty("transaction_amount").GetDecimal();
            return (status == "approved", monto, null);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
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
