using System.Security.Cryptography;
using System.Text;

namespace TrazeaBlock.Services;

public class HashingService
{
    public string CalcularSHA256(byte[] contenido)
    {
        var hash = SHA256.HashData(contenido);
        return Convert.ToHexString(hash).ToLower();
    }

    public string CalcularSHA256(string texto) =>
        CalcularSHA256(Encoding.UTF8.GetBytes(texto));

    public string FormatearCorto(string hash) =>
        hash.Length >= 16 ? hash[..8] + "..." + hash[^8..] : hash;
}
