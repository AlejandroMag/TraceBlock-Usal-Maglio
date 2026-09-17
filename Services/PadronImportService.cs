using System.Text;
using Npgsql;

namespace TrazeaBlock.Services;

// Importa el CSV del Registro Nacional de Sociedades y Asociaciones sin Fines de Lucro
// (datos.jus.gob.ar) a la tabla PadronOng.
//
// El CSV trae una fila por actividad, así que un mismo CUIT aparece repetido (~1,6 veces
// en promedio). Se deja una sola fila por CUIT, priorizando las actividades activas.
//
// Se deja que Postgres parsee el CSV con COPY: maneja las comillas y las comas dentro de
// los campos (hay más de 22.000 filas así) sin necesidad de una librería aparte.
public static class PadronImportService
{
    // Las columnas van en el mismo orden que el encabezado del CSV: COPY mapea por posición.
    private const string SqlStage = """
        CREATE TEMP TABLE padron_stage (
            cuit text, razon_social text, fecha_hora_contrato_social text, tipo_societario text,
            fecha_hora_actualizacion text, numero_inscripcion text,
            dom_fiscal_provincia text, dom_fiscal_localidad text, dom_fiscal_calle text,
            dom_fiscal_numero text, dom_fiscal_piso text, dom_fiscal_departamento text,
            dom_fiscal_cp text, dom_fiscal_estado_domicilio text,
            dom_legal_provincia text, dom_legal_localidad text, dom_legal_calle text,
            dom_legal_numero text, dom_legal_piso text, dom_legal_departamento text,
            dom_legal_cp text, dom_legal_estado_domicilio text,
            actividad_codigo text, actividad_descripcion text, actividad_orden text,
            actividad_estado text, actividad_vigencia text
        ) ON COMMIT DROP;
        """;

    private const string SqlCarga = """
        TRUNCATE TABLE "PadronOngs" RESTART IDENTITY;

        INSERT INTO "PadronOngs" ("Cuit","RazonSocial","TipoSocietario","Provincia","Localidad","FechaActualizacion")
        SELECT DISTINCT ON (btrim(s.cuit))
               btrim(s.cuit),
               coalesce(btrim(s.razon_social), ''),
               coalesce(btrim(s.tipo_societario), ''),
               coalesce(btrim(s.dom_legal_provincia), ''),
               coalesce(btrim(s.dom_legal_localidad), ''),
               CASE WHEN btrim(s.fecha_hora_actualizacion) ~ '^\d{4}-\d{2}-\d{2}-\d{2}:\d{2}$'
                    THEN to_timestamp(btrim(s.fecha_hora_actualizacion), 'YYYY-MM-DD-HH24:MI') END
        FROM padron_stage s
        WHERE btrim(s.cuit) ~ '^\d{11}$'
        ORDER BY btrim(s.cuit),
                 (btrim(s.actividad_estado) = 'AC') DESC,
                 s.actividad_orden NULLS LAST;
        """;

    public static async Task<(int organizaciones, int filasLeidas)> ImportarAsync(string connectionString, string rutaCsv)
    {
        if (!File.Exists(rutaCsv))
            throw new FileNotFoundException($"No se encontró el archivo: {rutaCsv}");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var cmd = new NpgsqlCommand(SqlStage, conn, tx))
            await cmd.ExecuteNonQueryAsync();

        // COPY del archivo tal cual (el StreamReader descarta el BOM del UTF-8).
        await using (var writer = await conn.BeginTextImportAsync(
            "COPY padron_stage FROM STDIN (FORMAT csv, HEADER true)"))
        using (var reader = new StreamReader(rutaCsv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            var buffer = new char[81920];
            int leidos;
            while ((leidos = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                await writer.WriteAsync(buffer, 0, leidos);
        }

        int filas;
        await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM padron_stage", conn, tx))
            filas = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        await using (var cmd = new NpgsqlCommand(SqlCarga, conn, tx))
            await cmd.ExecuteNonQueryAsync();

        int total;
        await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM \"PadronOngs\"", conn, tx))
            total = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        await tx.CommitAsync();
        return (total, filas);
    }
}
