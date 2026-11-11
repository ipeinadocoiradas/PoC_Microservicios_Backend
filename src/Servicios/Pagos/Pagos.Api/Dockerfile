# syntax=docker/dockerfile:1
# Imagen multi-stage de Pagos.Api. Contexto de construcción: raíz del repositorio.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Contratos/Contratos.Integracion/Contratos.Integracion.csproj src/Contratos/Contratos.Integracion/
COPY src/Compartido/Compartido.Aplicacion/Compartido.Aplicacion.csproj src/Compartido/Compartido.Aplicacion/
COPY src/Compartido/Compartido.Infraestructura/Compartido.Infraestructura.csproj src/Compartido/Compartido.Infraestructura/
COPY src/Compartido/DistributedTransactions.ServiceDefaults/DistributedTransactions.ServiceDefaults.csproj src/Compartido/DistributedTransactions.ServiceDefaults/
COPY src/Servicios/Pagos/Pagos.Domain/Pagos.Domain.csproj src/Servicios/Pagos/Pagos.Domain/
COPY src/Servicios/Pagos/Pagos.Application/Pagos.Application.csproj src/Servicios/Pagos/Pagos.Application/
COPY src/Servicios/Pagos/Pagos.Infrastructure/Pagos.Infrastructure.csproj src/Servicios/Pagos/Pagos.Infrastructure/
COPY src/Servicios/Pagos/Pagos.Api/Pagos.Api.csproj src/Servicios/Pagos/Pagos.Api/
RUN dotnet restore src/Servicios/Pagos/Pagos.Api/Pagos.Api.csproj

COPY src/ src/
RUN dotnet publish src/Servicios/Pagos/Pagos.Api/Pagos.Api.csproj \
    --configuration Release --no-restore --output /app/publicacion /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS ejecucion
WORKDIR /app
COPY --from=compilacion /app/publicacion .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pagos.Api.dll"]
