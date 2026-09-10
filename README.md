TraceBlock

Sistema de trazabilidad de donaciones para organizaciones sin fines de lucro (ONGs), con registro inmutable en blockchain y procesamiento de pagos a través de Mercado Pago.

Proyecto Final de Ingeniería en Informática — Universidad del Salvador, Facultad de Ingeniería.

Descripción

TraceBlock busca resolver la desconfianza a la hora de donar: hoy el donante no tiene forma de validar que su dinero llegó a la organización ni que fue usado como se prometió.

La plataforma ofrece una interfaz web para donantes y ONGs donde cada donación queda registrada de forma inmutable. Los pagos se procesan mediante Mercado Pago, enrutando el dinero directamente a la cuenta de cada ONG,  y se registra en la red Polygon para que no pueda falsificarse.

Características principales
Registro de donaciones con trazabilidad completa para el donante.
Procesamiento de pagos con Mercado Pago mediante Checkout Pro.
Split de pagos por OAuth (MP Connect): cada ONG cobra en su propia cuenta; la plataforma nunca custodia los fondos.
Registro inmutable de evidencia (hash SHA-256) en la red Polygon.
Panel de administración para las ONGs.
Stack tecnológico
Capa	Tecnología
Frontend	Blazor Server (.NET 8)
Backend	ASP.NET Core (.NET 8)
ORM	Entity Framework Core
Base de datos	SQLite (MVP_1) --> pasando a PostgreSQL 
Pagos	Mercado Pago (Checkout Pro + OAuth / MP Connect)

Blockchain	Polygon (testnet Amoy) --> Esto se esta por hacer


Requisitos previos
.NET 8 SDK
Una cuenta de Mercado Pago Developers con una aplicación creada (para las credenciales).
Configuración local

Los datos sensibles no se versionan en el repositorio. Se gestionan con User Secrets de .NET.

1. Clonar el repositorio
bash
git clone https://github.com/TU-USUARIO/TU-REPO.git
cd TU-REPO
2. Cargar los secretos

El proyecto ya tiene UserSecretsId configurado en el .csproj, así que no hace falta inicializar. Solo cargá los valores:

bash
dotnet user-secrets set "MercadoPago:ClientSecret" "tu-client-secret"
dotnet user-secrets set "MercadoPago:AccessToken" "tu-access-token"

Importante: estos valores no están en el repositorio. Al clonar en una máquina nueva hay que volver a cargarlos. Guardalos en un gestor de contraseñas, no en un archivo suelto. El ClientSecret puede regenerarse desde el panel de Mercado Pago si se pierde.

3. Configurar el resto de los parámetros

El archivo appsettings.json incluye la estructura de configuración. Los valores no sensibles (ClientId, PublicKey de test, RedirectUri) pueden ajustarse ahí o en appsettings.Development.json (este último no se versiona).


4. Aplicar migraciones y levantar
bash
dotnet ef database update
dotnet run



Autor

Alejandro Maglio — Ingeniería en Informática, Universidad del Salvador.

Licencia --> Estoy viendo que onda
