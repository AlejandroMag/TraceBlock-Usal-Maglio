using TrazeaBlock.Services.Interfaces;

namespace TrazeaBlock.Services.Mock;

public class MockGeoService : IGeoService
{
    public async Task<GeoResult> ValidarDistancia(
        double latProyecto, double lonProyecto,
        double latUsuario, double lonUsuario)
    {
        await Task.Delay(200);
        var distancia = GeoUtils.DistanciaKm(latProyecto, lonProyecto, latUsuario, lonUsuario);
        return new GeoResult(distancia <= GeoUtils.MaxDistanciaKm, distancia);
    }
}
