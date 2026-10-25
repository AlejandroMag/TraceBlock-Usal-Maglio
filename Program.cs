using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TrazeaBlock.Configuration;
using TrazeaBlock.Data;
using TrazeaBlock.Models;
using TrazeaBlock.Services;
using TrazeaBlock.Services.Blockchain;
using TrazeaBlock.Services.Interfaces;
using TrazeaBlock.Services.MercadoPago;
using TrazeaBlock.Services.Mock;

// Comando de mantenimiento para cargar el padrón oficial de ONGs:
//   dotnet run -- importar-padron "C:\ruta\registro-nacional.csv"
var rutaPadron = args.Length >= 2 && args[0] == "importar-padron" ? args[1] : null;

var builder = WebApplication.CreateBuilder(rutaPadron is null ? args : Array.Empty<string>());

if (rutaPadron is not null)
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Falta la connection string 'DefaultConnection'.");

    // Aseguramos que el esquema exista antes de importar.
    var opciones = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options;
    await using (var db = new AppDbContext(opciones))
        await db.Database.MigrateAsync();

    Console.WriteLine($"Importando padrón desde: {rutaPadron}");
    var reloj = System.Diagnostics.Stopwatch.StartNew();
    var (orgs, filas) = await PadronImportService.ImportarAsync(cs, rutaPadron);
    Console.WriteLine($"Listo en {reloj.Elapsed.TotalSeconds:N1}s — {filas:N0} filas leídas, " +
                      $"{orgs:N0} organizaciones cargadas (una por CUIT).");
    return;
}

// Render (y otros hostings) asignan el puerto por la variable PORT.
var puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(puerto))
    builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");

// 1. DbContext (PostgreSQL). El retry cubre el arranque en Docker, donde la app
// puede levantar antes de que la base acepte conexiones, y el despertar de Neon,
// que suspende la base tras unos minutos sin uso.
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure()));

// 2. Configuración de validaciones (fuertemente tipada)
var validacionesConfig = builder.Configuration
    .GetSection("Validaciones").Get<ValidacionesConfig>()
    ?? new ValidacionesConfig();
builder.Services.AddSingleton(validacionesConfig);

// 3. Servicios externos (mock por ahora; se swapean por reales cuando los flags sean true)
builder.Services.AddScoped<IGeoService, MockGeoService>();

// Blockchain: Polygon real si el flag está activo, si no el simulado. Es singleton porque el
// servicio real serializa los envíos de la única billetera de la plataforma.
var blockchainOptions = builder.Configuration.GetSection("Blockchain").Get<BlockchainOptions>()
    ?? new BlockchainOptions();
builder.Services.AddSingleton(blockchainOptions);
if (blockchainOptions.UsarBlockchainReal)
{
    builder.Services.AddSingleton<IBlockchainService, PolygonBlockchainService>();
    builder.Services.AddHostedService<AnclajePendienteService>(); // retoma registros que quedaron pendientes
}
else
{
    builder.Services.AddSingleton<IBlockchainService, MockBlockchainService>();
}

// MercadoPago: credenciales tipadas (Access Token via user-secrets)
var mpOptions = builder.Configuration.GetSection("MercadoPago").Get<MercadoPagoOptions>()
    ?? new MercadoPagoOptions();
builder.Services.AddSingleton(mpOptions);

// Cobro de respaldo, solo para ValidarPagoMercadoPago = false (correr sin credenciales).
// Con el flag en true el cobro va por Checkout Pro (MercadoPagoConexionService) y lo procesa
// MercadoPago, con credenciales de prueba pero contra su infraestructura real.
builder.Services.AddScoped<IPagoService, MockPagoService>();

// Marketplace / OAuth de MercadoPago (conexión por ONG)
builder.Services.AddHttpClient<MercadoPagoConexionService>();
builder.Services.AddSingleton<OAuthStateStore>();

// 4. Servicios de dominio
builder.Services.AddSingleton<HashingService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DonacionService>();
builder.Services.AddScoped<ProyectoService>();
builder.Services.AddScoped<EvidenciaService>();
builder.Services.AddScoped<AppState>();

// 5. Autenticación por cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/";
        o.AccessDeniedPath = "/";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Las claves que cifran las cookies se guardan en la base: el hosting no tiene disco
// persistente y, si se regeneraran en cada reinicio, se desloguearía a todos.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("TraceBlock");

// En producción la app corre detrás de un proxy (Render, Cloudflare) que recibe el HTTPS y
// le reenvía la petición por HTTP. Estas cabeceras le dicen el esquema original: sin ellas
// la app se cree en http y arma mal las URLs de vuelta de MercadoPago. Los proxies del
// hosting no tienen IPs fijas conocidas, por eso se aceptan de cualquier origen.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Services.AddCascadingAuthenticationState();

// 6. Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// 7. Build
var app = builder.Build();

// 8. Migrate + Seed: aplica las migraciones pendientes y después carga los datos de demo
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

// Si la blockchain real está activa, se construye ahora para que una configuración
// incompleta (clave privada, contrato) falle al arrancar y no en la primera donación.
if (blockchainOptions.UsarBlockchainReal)
    _ = app.Services.GetRequiredService<IBlockchainService>();

// 9. Middleware
app.UseForwardedHeaders(); // primero: el resto tiene que ver el esquema real (https)
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// ---- Endpoints de autenticación (necesarios para SignInAsync con cookies en Blazor Server) ----
static async Task SignIn(HttpContext ctx, Usuario usuario)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new(ClaimTypes.Name, usuario.Nombre),
        new(ClaimTypes.Email, usuario.Email),
        new(ClaimTypes.Role, usuario.Rol.ToString())
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
        new AuthenticationProperties { IsPersistent = true });
}

static string DestinoPorRol(Usuario usuario)
{
    if (usuario.Rol == RolUsuario.Organizacion)
        return usuario.Organizacion?.Estado == EstadoValidacion.Validada
            ? "/ong/dashboard" : "/pendiente";
    return "/dashboard";
}

app.MapPost("/auth/login", async (HttpContext ctx, AuthService auth) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var usuario = await auth.LoginAsync(email, password);
    if (usuario is null)
    {
        var rt = string.IsNullOrEmpty(returnUrl) ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        return Results.Redirect($"/login?error=1{rt}");
    }

    await SignIn(ctx, usuario);

    if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//"))
        return Results.Redirect(returnUrl);

    return Results.Redirect(DestinoPorRol(usuario));
});

app.MapPost("/auth/registro-donante", async (HttpContext ctx, AuthService auth) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var nombre = form["nombre"].ToString();
    var apellido = form["apellido"].ToString();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var confirm = form["confirm"].ToString();

    if (password != confirm)
        return Results.Redirect($"/registro?tab=donante&error={Uri.EscapeDataString("Las contraseñas no coinciden.")}");

    var (exito, error) = await auth.RegistrarDonanteAsync(nombre, apellido, email, password);
    if (!exito)
        return Results.Redirect($"/registro?tab=donante&error={Uri.EscapeDataString(error ?? "Error al registrar.")}");

    var usuario = await auth.LoginAsync(email, password);
    if (usuario is not null) await SignIn(ctx, usuario);
    return Results.Redirect("/dashboard");
});

app.MapPost("/auth/registro-ong", async (HttpContext ctx, AuthService auth) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var nombreOrg = form["nombreOrg"].ToString();
    var emailInstitucional = form["emailInstitucional"].ToString();
    var cuit = form["cuit"].ToString();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var confirm = form["confirm"].ToString();

    if (password != confirm)
        return Results.Redirect($"/registro?tab=ong&error={Uri.EscapeDataString("Las contraseñas no coinciden.")}");

    var (exito, error) = await auth.RegistrarONGAsync(nombreOrg, emailInstitucional, cuit, email, password);
    if (!exito)
        return Results.Redirect($"/registro?tab=ong&error={Uri.EscapeDataString(error ?? "Error al registrar.")}");

    var usuario = await auth.LoginAsync(email, password);
    if (usuario is not null) await SignIn(ctx, usuario);
    return Results.Redirect("/pendiente");
});

app.MapPost("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// ---- OAuth de MercadoPago (Marketplace): la ONG conecta su propia cuenta ----
app.MapGet("/mp/oauth/connect", async (HttpContext ctx, AuthService auth,
    MercadoPagoConexionService conexion, OAuthStateStore store) =>
{
    var userId = ctx.User.GetUserId();
    if (userId == 0) return Results.Redirect("/");
    if (!conexion.ConfigDisponible) return Results.Redirect("/ong/dashboard?mp=config");
    var org = await auth.ObtenerOrganizacionPorUsuarioAsync(userId);
    if (org is null) return Results.Redirect("/");
    var state = store.Crear(org.Id);
    return Results.Redirect(conexion.GetAuthorizationUrl(state));
}).RequireAuthorization();

app.MapGet("/mp/cb", async (MercadoPagoConexionService conexion, OAuthStateStore store,
    string? code, string? state, string? error) =>
{
    if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        return Results.Redirect("/ong/dashboard?mp=error");
    var orgId = store.Consumir(state);
    if (orgId is null) return Results.Redirect("/ong/dashboard?mp=error");
    var resultado = await conexion.ConectarAsync(orgId.Value, code);
    return Results.Redirect("/ong/dashboard?mp=" + resultado switch
    {
        ResultadoConexionMp.Conectada => "ok",
        ResultadoConexionMp.Renovada => "renovada",
        ResultadoConexionMp.CuentaDistinta => "distinta",
        ResultadoConexionMp.CuentaEnUso => "enuso",
        _ => "error"
    });
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
