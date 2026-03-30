# BEGIN-FEAT::BE-672::2026-03-28::AHL::Dockerfile multi-stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Monitoreo.Worker/ ./Monitoreo.Worker/
WORKDIR /src/Monitoreo.Worker
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "Monitoreo.Worker.dll"]
# END-FEAT::BE-672::2026-03-28::AHL::Dockerfile multi-stage
