namespace TrazeaBlock.Services;

// Estado por circuito (scoped) usado para coordinar el modal de login
// entre componentes que no comparten jerarquía directa.
public class AppState
{
    public event Action? OnShowLogin;

    // Indica en qué pestaña abrir el modal: "login" o "registro"
    public string TabInicial { get; private set; } = "login";

    public void ShowLogin(string tab = "login")
    {
        TabInicial = tab;
        OnShowLogin?.Invoke();
    }
}
