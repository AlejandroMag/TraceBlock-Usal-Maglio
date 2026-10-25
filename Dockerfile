# Imagen de TraceBlock para Render (o cualquier hosting de contenedores).
# Etapa 1 compila con el SDK; etapa 2 corre solo con el runtime, que es mucho más liviana.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero solo el .csproj: si las dependencias no cambian, Docker reutiliza esta capa
# y no vuelve a descargar los paquetes en cada build.
COPY TrazeaBlock.csproj .
RUN dotnet restore TrazeaBlock.csproj

COPY . .
RUN dotnet publish TrazeaBlock.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Production: sin credenciales de demo en pantalla ni páginas de error detalladas.
# El puerto lo asigna el hosting con la variable PORT (ver Program.cs); 8080 si no la define.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Usuario sin privilegios que trae la imagen oficial.
USER $APP_UID

ENTRYPOINT ["dotnet", "TrazeaBlock.dll"]
