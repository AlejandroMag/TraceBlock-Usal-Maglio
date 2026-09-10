namespace TrazeaBlock.Services.Interfaces;

public interface ICenocService
{
    Task<CenocResult> ValidarOrganizacion(string cuit, byte[] pdfBytes);
}

public record CenocResult(bool Registrada, string NombreCenoc, string? Error);
