# BEGIN-FEAT::BE-672::2026-03-28::AHL::Dockerfile multi-stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Monitoreo.Worker/ ./Monitoreo.Worker/
# FEAT::BE-672::2026-09-02::AHL::Templates viven en la raiz del repo (templates_xml_json); se copian para que el publish los hornee en la imagen (respaldo si falta el mount)
COPY templates_xml_json/ ./Templates/
WORKDIR /src/Monitoreo.Worker
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
RUN apk add --no-cache tzdata
COPY --from=build /app/publish .
ENV DOTNET_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "Monitoreo.Worker.dll"]
# END-FEAT::BE-672::2026-03-28::AHL::Dockerfile multi-stage
