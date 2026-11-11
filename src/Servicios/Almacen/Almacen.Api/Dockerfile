# syntax=docker/dockerfile:1
# Imagen multi-stage de Almacen.Api. Contexto de construcción: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Contratos/Contratos.Integracion/Contratos.Integracion.csproj src/Contratos/Contratos.Integracion/
COPY src/Compartido/Compartido.Aplicacion/Compartido.Aplicacion.csproj src/Compartido/Compartido.Aplicacion/
COPY src/Compartido/Compartido.Infraestructura/Compartido.Infraestructura.csproj src/Compartido/Compartido.Infraestructura/
COPY src/Compartido/DistributedTransactions.ServiceDefaults/DistributedTransactions.ServiceDefaults.csproj src/Compartido/DistributedTransactions.ServiceDefaults/
COPY src/Servicios/Almacen/Almacen.Domain/Almacen.Domain.csproj src/Servicios/Almacen/Almacen.Domain/
COPY src/Servicios/Almacen/Almacen.Application/Almacen.Application.csproj src/Servicios/Almacen/Almacen.Application/
COPY src/Servicios/Almacen/Almacen.Infrastructure/Almacen.Infrastructure.csproj src/Servicios/Almacen/Almacen.Infrastructure/
COPY src/Servicios/Almacen/Almacen.Api/Almacen.Api.csproj src/Servicios/Almacen/Almacen.Api/
RUN dotnet restore src/Servicios/Almacen/Almacen.Api/Almacen.Api.csproj

COPY src/ src/
RUN dotnet publish src/Servicios/Almacen/Almacen.Api/Almacen.Api.csproj \
    --configuration Release --no-restore --output /app/publicacion /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ejecucion
WORKDIR /app
COPY --from=compilacion /app/publicacion .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Almacen.Api.dll"]
