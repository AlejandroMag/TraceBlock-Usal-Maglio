namespace TrazeaBlock.Models;

// Una fila por CUIT del Registro Nacional de Sociedades y Asociaciones sin Fines de Lucro
// (datos.jus.gob.ar). Se carga con el comando "importar-padron" y se usa para validar
// que la organización que se registra exista realmente ante el Estado.
public class PadronOng
{
    public int Id { get; set; }
    public string Cuit { get; set; } = "";          // 11 dígitos, sin guiones
    public string RazonSocial { get; set; } = "";
    public string TipoSocietario { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string Localidad { get; set; } = "";
    public DateTime? FechaActualizacion { get; set; }
}
