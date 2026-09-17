using System.Globalization;
using System.Text;

namespace TrazeaBlock.Services;

// Compara el dominio del mail institucional con la razón social del padrón oficial.
// Ejemplo: "contacto@fundacionagraria.org.ar" contra "FUNDACION FEDERACION AGRARIA
// ARGENTINA" -> el dominio "fundacionagraria" contiene "agraria" -> coincide.
public static class SimilitudDominio
{
    // Palabras de forma jurídica o conectores: no identifican a la organización.
    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        "asociacion", "civil", "fundacion", "simple", "extranjera", "argentina",
        "de", "del", "la", "las", "los", "el", "y", "e", "en", "para", "por",
        "con", "sin", "fines", "lucro", "mutual", "cooperativa", "ltda", "limitada"
    };

    // Sufijos públicos habituales: se descartan para quedarnos con el núcleo del dominio.
    private static readonly HashSet<string> Sufijos = new(StringComparer.Ordinal)
    {
        "ar", "com", "org", "net", "edu", "gob", "gov", "int", "mil",
        "coop", "info", "biz", "io", "app", "online", "site", "ong"
    };

    /// <summary>
    /// Indica si el dominio del mail se parece a la razón social.
    /// <paramref name="palabrasEsperadas"/> devuelve las palabras significativas, para el mensaje de error.
    /// </summary>
    public static bool Coincide(string emailInstitucional, string razonSocial, out string[] palabrasEsperadas)
    {
        palabrasEsperadas = Array.Empty<string>();

        var nucleo = NucleoDominio(emailInstitucional);
        if (string.IsNullOrEmpty(nucleo)) return false;

        var palabras = Palabras(razonSocial);
        var significativas = palabras.Where(p => p.Length >= 4).ToArray();
        palabrasEsperadas = significativas;

        // Si la razón social no tiene ninguna palabra identificatoria (ej. "ONG LA PAZ"),
        // no hay nada contra qué comparar: no bloqueamos.
        if (significativas.Length == 0) return true;

        // Coincidencia directa: alguna palabra significativa aparece en el dominio.
        if (significativas.Any(p => nucleo.Contains(p, StringComparison.Ordinal))) return true;

        // Coincidencia por siglas: el dominio son las iniciales de la razón social completa,
        // incluidas las palabras de forma jurídica (ej. "acdp" = Asociacion Civil Del Parque).
        var todas = PalabrasCrudas(razonSocial);
        if (todas.Length >= 3)
        {
            var siglas = string.Concat(todas.Select(p => p[0]));
            if (siglas.Length >= 3)
            {
                // El dominio contiene las siglas completas ("acdp").
                if (nucleo.Contains(siglas, StringComparison.Ordinal)) return true;
                // O son las siglas de las primeras palabras, lo más habitual:
                // "edns" para "Escuelas De Nuestra Señora Asociacion Civil" -> "ednsac".
                if (nucleo.Length >= 3 && siglas.StartsWith(nucleo, StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    // "contacto@fundacionagraria.org.ar" -> "fundacionagraria"
    private static string NucleoDominio(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return "";
        var dominio = email.Split('@').Last();
        var etiquetas = Normalizar(dominio.Replace('.', ' '))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(e => !Sufijos.Contains(e));
        return string.Concat(etiquetas);
    }

    // Todas las palabras normalizadas, sin filtrar: sirven para armar las siglas.
    private static string[] PalabrasCrudas(string razonSocial) =>
        Normalizar(razonSocial).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // "FUNDACION FEDERACION AGRARIA" -> ["federacion", "agraria"]  (sin palabras vacías)
    private static string[] Palabras(string razonSocial) =>
        Normalizar(razonSocial)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 1 && !Vacias.Contains(p))
            .ToArray();

    // Minúsculas, sin tildes y solo letras/números (los separadores quedan como espacios).
    private static string Normalizar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var sinTildes = texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        var sb = new StringBuilder();
        foreach (var c in sinTildes)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else sb.Append(' ');
        }
        return sb.ToString();
    }
}
