namespace TrazeaBlock.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public RolUsuario Rol { get; set; } = RolUsuario.Donante;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public Organizacion? Organizacion { get; set; }
    public List<Donacion> Donaciones { get; set; } = new();
}

public enum RolUsuario { Donante, Organizacion, Admin }
