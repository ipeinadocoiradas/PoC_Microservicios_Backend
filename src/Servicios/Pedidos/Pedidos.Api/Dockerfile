# syntax=docker/dockerfile:1
# Imagen multi-stage de Pedidos.Api. Contexto de construcción: raíz del repositorio.

# ── Etapa 1: restauración y publicación ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src

# Se copian primero los .csproj para que la capa de "restore" quede en caché
# mientras no cambien las dependencias.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Contratos/Contratos.Integracion/Contratos.Integracion.csproj src/Contratos/Contratos.Integracion/
COPY src/Compartido/Compartido.Aplicacion/Compartido.Aplicacion.csproj src/Compartido/Compartido.Aplicacion/
COPY src/Compartido/Compartido.Infraestructura/Compartido.Infraestructura.csproj src/Compartido/Compartido.Infraestructura/
COPY src/Compartido/DistributedTransactions.ServiceDefaults/DistributedTransactions.ServiceDefaults.csproj src/Compartido/DistributedTransactions.ServiceDefaults/
COPY src/Servicios/Pedidos/Pedidos.Domain/Pedidos.Domain.csproj src/Servicios/Pedidos/Pedidos.Domain/
COPY src/Servicios/Pedidos/Pedidos.Application/Pedidos.Application.csproj src/Servicios/Pedidos/Pedidos.Application/
COPY src/Servicios/Pedidos/Pedidos.Infrastructure/Pedidos.Infrastructure.csproj src/Servicios/Pedidos/Pedidos.Infrastructure/
COPY src/Servicios/Pedidos/Pedidos.Api/Pedidos.Api.csproj src/Servicios/Pedidos/Pedidos.Api/
RUN dotnet restore src/Servicios/Pedidos/Pedidos.Api/Pedidos.Api.csproj

COPY src/ src/
RUN dotnet publish src/Servicios/Pedidos/Pedidos.Api/Pedidos.Api.csproj \
    --configuration Release --no-restore --output /app/publicacion /p:UseAppHost=false

# ── Etapa 2: ejecución (solo runtime de ASP.NET Core, usuario sin privilegios) ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ejecucion
WORKDIR /app
COPY --from=compilacion /app/publicacion .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pedidos.Api.dll"]
