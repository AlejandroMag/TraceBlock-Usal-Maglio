using System.Collections.Concurrent;

namespace TrazeaBlock.Services.MercadoPago;

// Guarda temporalmente el mapping state -> organizacionId durante el flujo OAuth,
// para validar el retorno (anti-CSRF) sin depender de la cookie de sesión en el callback.
public class OAuthStateStore
{
    private readonly ConcurrentDictionary<string, (int orgId, DateTime creado)> _states = new();

    public string Crear(int orgId)
    {
        var state = Guid.NewGuid().ToString("N");
        _states[state] = (orgId, DateTime.UtcNow);
        return state;
    }

    public int? Consumir(string state)
    {
        if (_states.TryRemove(state, out var v) && DateTime.UtcNow - v.creado < TimeSpan.FromMinutes(10))
            return v.orgId;
        return null;
    }
}
