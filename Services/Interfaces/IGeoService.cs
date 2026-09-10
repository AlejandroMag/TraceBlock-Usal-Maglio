namespace TrazeaBlock.Services.Interfaces;

public interface IGeoService
{
    Task<GeoResult> ValidarDistancia(
        double latProyecto, double lonProyecto,
        double latUsuario, double lonUsuario);
}

public record GeoResult(bool Valido, double DistanciaKm);
