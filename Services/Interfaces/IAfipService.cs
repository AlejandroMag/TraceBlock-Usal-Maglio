namespace TrazeaBlock.Services.Interfaces;

public interface IAfipService
{
    Task<AfipResult> ValidarCUIT(string cuit);
}

public record AfipResult(bool Valido, string RazonSocial, string? Error);
