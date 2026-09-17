using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TrazeaBlock.Configuration;
using TrazeaBlock.Data;
using TrazeaBlock.Models;
using TrazeaBlock.Services;
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

// 1. DbContext (PostgreSQL). El retry cubre el arranque en Docker, donde la app
// puede levantar antes de que la base acepte conexiones.
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
builder.Services.AddScoped<IBlockchainService, MockBlockchainService>();

// MercadoPago: credenciales tipadas (Access Token via user-secrets)
var mpOptions = builder.Configuration.GetSection("MercadoPago").Get<MercadoPagoOptions>()
    ?? new MercadoPagoOptions();
builder.Services.AddSingleton(mpOptions);

// Pago: real (MercadoPago) si el flag está activo, si no el mock.
if (validacionesConfig.ValidarPagoMercadoPago)
    builder.Services.AddScoped<IPagoService, MercadoPagoPagoService>();
else
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

// 9. Middleware
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
    var (ok, _) = await conexion.ConectarAsync(orgId.Value, code);
    return Results.Redirect(ok ? "/ong/dashboard?mp=ok" : "/ong/dashboard?mp=error");
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
