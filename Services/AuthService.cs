using Microsoft.EntityFrameworkCore;
using TrazeaBlock.Data;
using TrazeaBlock.Models;

namespace TrazeaBlock.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    private static readonly string[] DominiosPersonales =
        { "gmail.com", "hotmail.com", "outlook.com", "yahoo.com", "live.com" };

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Organizacion?> ObtenerOrganizacionPorUsuarioAsync(int usuarioId)
    {
        return await _db.Organizaciones
            .FirstOrDefaultAsync(o => o.UsuarioId == usuarioId);
    }

    public async Task<Usuario?> LoginAsync(string email, string password)
    {
        email = (email ?? "").Trim().ToLower();
        var usuario = await _db.Usuarios
            .Include(u => u.Organizacion)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (usuario is null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash)) return null;
        return usuario;
    }

    public async Task<(bool exito, string? error)> RegistrarDonanteAsync(
        string nombre, string apellido, string email, string password)
    {
        email = (email ?? "").Trim().ToLower();

        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido))
            return (false, "Nombre y apellido son obligatorios.");
        if (string.IsNullOrWhiteSpace(email))
            return (false, "El email es obligatorio.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (false, "La contraseña debe tener al menos 6 caracteres.");

        if (await _db.Usuarios.AnyAsync(u => u.Email == email))
            return (false, "Ya existe un usuario registrado con ese email.");

        var usuario = new Usuario
        {
            Nombre = nombre.Trim(),
            Apellido = apellido.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Rol = RolUsuario.Donante
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool exito, string? error)> RegistrarONGAsync(
        string nombreOrg, string emailInstitucional, string cuit,
        string email, string password)
    {
        email = (email ?? "").Trim().ToLower();
        emailInstitucional = (emailInstitucional ?? "").Trim().ToLower();

        if (string.IsNullOrWhiteSpace(nombreOrg))
            return (false, "El nombre de la organización es obligatorio.");
        if (string.IsNullOrWhiteSpace(email))
            return (false, "El email de acceso es obligatorio.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (false, "La contraseña debe tener al menos 6 caracteres.");

        if (await _db.Usuarios.AnyAsync(u => u.Email == email))
            return (false, "Ya existe un usuario registrado con ese email.");

        var dominio = emailInstitucional.Contains('@')
            ? emailInstitucional.Split('@').Last()
            : "";
        if (DominiosPersonales.Contains(dominio))
            return (false, "El email institucional no puede ser de un dominio personal (gmail, hotmail, outlook, yahoo, live).");

        if (!ValidarDigitoVerificadorCUIT(cuit))
            return (false, "El CUIT ingresado no es válido (dígito verificador incorrecto).");

        // El CUIT tiene que figurar en el padrón oficial de organizaciones sin fines de lucro.
        var padron = await BuscarEnPadronAsync(cuit);
        if (padron is null)
            return (false, "El CUIT no figura en el Registro Nacional de Sociedades y Asociaciones sin Fines de Lucro. " +
                           "Verificá el número o consultá si tu organización está inscripta.");

        // El dominio del mail institucional tiene que parecerse a la razón social del padrón.
        if (!SimilitudDominio.Coincide(emailInstitucional, padron.RazonSocial, out var esperadas))
        {
            var pistas = esperadas.Length > 0 ? string.Join(", ", esperadas) : padron.RazonSocial;
            return (false, $"El dominio del mail institucional no se corresponde con «{padron.RazonSocial}». " +
                           $"Se esperaba que incluyera alguna de estas palabras: {pistas}.");
        }

        var usuario = new Usuario
        {
            Nombre = nombreOrg.Trim(),
            Apellido = "",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Rol = RolUsuario.Organizacion
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        // Validada en el acto: el padrón oficial ya acredita a la organización.
        var org = new Organizacion
        {
            UsuarioId = usuario.Id,
            NombreOrganizacion = nombreOrg.Trim(),
            CUIT = cuit.Trim(),
            EmailInstitucional = emailInstitucional,
            Estado = EstadoValidacion.Validada,
            FechaValidacion = DateTime.UtcNow,
            CiudadSede = padron.Localidad
        };
        _db.Organizaciones.Add(org);
        await _db.SaveChangesAsync();

        return (true, null);
    }

    /// <summary>Busca un CUIT en el padrón oficial. Acepta el CUIT con o sin guiones.</summary>
    public async Task<PadronOng?> BuscarEnPadronAsync(string cuit)
    {
        var limpio = new string((cuit ?? "").Where(char.IsDigit).ToArray());
        if (limpio.Length != 11) return null;
        return await _db.PadronOngs.FirstOrDefaultAsync(p => p.Cuit == limpio);
    }

    public bool ValidarDigitoVerificadorCUIT(string cuit)
    {
        if (string.IsNullOrWhiteSpace(cuit)) return false;

        var limpio = cuit.Replace("-", "").Replace(" ", "").Trim();
        if (limpio.Length != 11) return false;
        if (!limpio.All(char.IsDigit)) return false;

        int[] multiplicadores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        int suma = 0;
        for (int i = 0; i < 10; i++)
            suma += (limpio[i] - '0') * multiplicadores[i];

        int resto = suma % 11;
        int verificador = 11 - resto;
        if (verificador == 11) verificador = 0;
        else if (verificador == 10) verificador = 9;

        int digitoReal = limpio[10] - '0';
        return verificador == digitoReal;
    }

}
