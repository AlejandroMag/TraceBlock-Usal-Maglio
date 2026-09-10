namespace TrazeaBlock.Services;

// Cálculo real de distancia geográfica (fórmula de Haversine).
// Fuente única usada tanto por el servidor (ProyectoService/MockGeoService)
// como por la UI del wizard para el feedback en vivo.
public static class GeoUtils
{
    // Distancia máxima permitida entre la ubicación de la ONG y el proyecto.
    public const double MaxDistanciaKm = 10;

    public static double DistanciaKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // radio terrestre en km
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRad(double grados) => grados * Math.PI / 180.0;
}
