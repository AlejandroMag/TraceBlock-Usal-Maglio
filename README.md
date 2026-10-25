# TraceBlock

Sistema de trazabilidad de donaciones para organizaciones sin fines de lucro (ONGs), con registro inmutable en blockchain y procesamiento de pagos a través de Mercado Pago.

Proyecto Final de Ingeniería en Informática — Universidad del Salvador, Facultad de Ingeniería.

## Descripción

TraceBlock busca resolver la desconfianza a la hora de donar: hoy el donante no tiene forma de validar que su dinero llegó a la organización ni que fue usado como se prometió.

La plataforma ofrece una interfaz web para donantes y ONGs donde cada donación queda registrada de forma inmutable. Los pagos se procesan mediante Mercado Pago, enrutando el dinero directamente a la cuenta de cada ONG, y se registra en la red Polygon para que no pueda falsificarse.

**La plataforma nunca custodia los fondos.** Esa es la idea central: trazabilidad sin custodia.

## Características principales

- Registro de donaciones con trazabilidad completa para el donante.
- Procesamiento de pagos con Mercado Pago mediante Checkout Pro.
- Split de pagos por OAuth (MP Connect): cada ONG cobra en su propia cuenta, el 100% del monto.
- Registro inmutable de donaciones y de evidencia (hash SHA-256) en la red Polygon.
- Validación de ONGs contra el padrón nacional de organizaciones (CUIT + razón social).
- Panel de administración para las ONGs.

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| Frontend | Blazor Server (.NET 8) |
| Backend | ASP.NET Core (.NET 8) |
| ORM | Entity Framework Core 8 |
| Base de datos | PostgreSQL 16 (en Docker) |
| Pagos | Mercado Pago (Checkout Pro + OAuth / MP Connect) |
| Blockchain | Polygon, testnet Amoy (Nethereum) |

### Contrato desplegado

`TrazeaBlockRegistry` en Polygon Amoy, con el código fuente verificado:

https://amoy.polygonscan.com/address/0xa328599bDc052B0DE90E223dD059ad0b22FCd36F

Solo la billetera de la plataforma puede escribir; cualquiera puede leer. **No se guarda ningún dato personal en la cadena**: únicamente identificadores anónimos, montos en centavos y hashes SHA-256.

## Requisitos previos

- .NET 8 SDK
- Docker (para PostgreSQL)
- Una cuenta de Mercado Pago Developers con una aplicación creada (para las credenciales).
- Una billetera con POL de testnet, si se quiere registrar en Polygon de verdad.

## Configuración local

Los datos sensibles no se versionan en el repositorio. Se gestionan con User Secrets de .NET.

### 1. Clonar el repositorio

```bash
git clone https://github.com/TU-USUARIO/TU-REPO.git
cd TU-REPO
```

### 2. Levantar la base de datos

```bash
docker compose up -d db
```

### 3. Cargar los secretos

El proyecto ya tiene `UserSecretsId` configurado en el `.csproj`, así que no hace falta inicializar. Solo cargá los valores:

```bash
dotnet user-secrets set "MercadoPago:ClientSecret" "tu-client-secret"
dotnet user-secrets set "MercadoPago:AccessToken" "tu-access-token"
dotnet user-secrets set "Blockchain:PrivateKey" "clave-privada-de-la-billetera"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=trazeablock;Username=trazeablock;Password=..."
```

> **Importante:** estos valores no están en el repositorio. Al clonar en una máquina nueva hay que volver a cargarlos. Guardalos en un gestor de contraseñas, no en un archivo suelto. El `ClientSecret` puede regenerarse desde el panel de Mercado Pago si se pierde.
>
> La billetera de `Blockchain:PrivateKey` es **solo de testnet**. Nunca cargarle fondos reales.

### 4. Configurar el resto de los parámetros

El archivo `appsettings.json` incluye la estructura de configuración. Los valores no sensibles (ClientId, PublicKey de test, RedirectUri, dirección del contrato, RPC) pueden ajustarse ahí o en `appsettings.Development.json` (este último no se versiona).

Dos interruptores útiles para correr la app sin credenciales:

- `Validaciones:ValidarPagoMercadoPago: false` → pagos simulados, sin Mercado Pago.
- `Blockchain:UsarBlockchainReal: false` → registro simulado, sin red ni billetera.

### 5. Importar el padrón de ONGs

**Este paso es obligatorio en una instalación nueva**: sin el padrón cargado, ninguna ONG puede registrarse, porque el alta valida el CUIT contra él.

```bash
dotnet run -- importar-padron "ruta/al/regNac_ONG.csv"
```

El CSV es el padrón nacional de organizaciones publicado como dato abierto. La importación trunca la tabla y la vuelve a llenar, así que se puede repetir para actualizarla.

### 6. Levantar

```bash
dotnet run
```

Las migraciones se aplican solas al arrancar.

> Para probar el flujo real de Mercado Pago hace falta exponer la app por HTTPS público (por ejemplo con Dev Tunnels), porque MP no acepta `localhost` como URL de redirect. La URL del túnel debe coincidir **exactamente** con `MercadoPago:RedirectUri` y con la configurada en el panel de Mercado Pago.

## Despliegue (Render + Neon)

La app se publica como contenedor con el `Dockerfile` del repositorio. Render no tiene servidores en Sudamérica: conviene la región **Virginia** en Render y **`aws-us-east-1`** en Neon, para que la app y la base queden cerca.

### Base de datos en Neon

Neon entrega una URL tipo `postgresql://usuario:clave@host/base?sslmode=require`, pero Npgsql necesita el formato de pares clave=valor:

```
Host=ep-xxxx.us-east-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=Require
```

Usar el host **directo**, no el que termina en `-pooler`. Las migraciones se aplican solas al arrancar la app.

El padrón se importa una sola vez, desde la máquina local, apuntando a Neon:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=ep-xxxx...;SSL Mode=Require"
dotnet run -- importar-padron "C:\ruta\regNac_ONG.csv"
```

### Servicio web en Render

Crear un **Web Service** desde el repositorio de GitHub, con runtime **Docker**. Variables de entorno:

| Variable | Valor |
|---|---|
| `ConnectionStrings__DefaultConnection` | La conexión a Neon, en el formato de arriba |
| `MercadoPago__ClientSecret` | El de la aplicación del Integrador |
| `MercadoPago__RedirectUri` | `https://<servicio>.onrender.com/mp/cb` (y la misma en el panel de MercadoPago) |
| `Blockchain__PrivateKey` | La clave de la billetera de la plataforma, **solo de testnet** |

`PORT` lo define Render y la app lo toma sola. El resto de la configuración pública está en `appsettings.json`.

> En el plan gratuito, Render duerme el servicio tras 15 minutos sin tráfico y tarda alrededor de un minuto en despertar. Mientras duerme, tampoco corre el reintento de anclajes pendientes en Polygon.

## Operación

### Cambiar la cuenta de MercadoPago de una ONG

Por seguridad, la cuenta de MercadoPago que conecta una ONG queda fija: desde el panel solo se puede renovar la conexión con **esa misma cuenta**. Así, quien obtenga la contraseña de una ONG no puede desviar sus donaciones a otra cuenta. Además, una cuenta de MercadoPago no puede cobrar para dos ONGs a la vez.

Si una ONG necesita cambiarla, lo hace el administrador directo en la base:

```sql
-- 1. Identificar la ONG y ver qué cuenta tiene hoy
SELECT o."Id", o."NombreOrganizacion", o."CUIT", m."MpUserId", m."FechaConexion"
FROM "Organizaciones" o
LEFT JOIN "OrgMercadoPagos" m ON m."OrganizacionId" = o."Id"
WHERE o."NombreOrganizacion" ILIKE '%nombre%';

-- 2. Desasociar la cuenta actual (reemplazar 123 por el Id de la ONG)
DELETE FROM "OrgMercadoPagos" WHERE "OrganizacionId" = 123;
```

Después, la ONG entra a su panel y conecta la cuenta nueva con el botón de siempre.

> Conviene hacerlo cuando la ONG no tenga donaciones en curso: un pago iniciado con la cuenta vieja no se podrá confirmar después del cambio, porque el sistema verifica que lo haya cobrado la cuenta asociada.

## Autor

Alejandro Maglio — Ingeniería en Informática, Universidad del Salvador.

## Licencia

MIT — ver [LICENSE](LICENSE). El contrato `TrazeaBlockRegistry` se publica bajo la misma licencia.
