# syntax=docker/dockerfile:1
# Food-Assistant server image (ADR-0007): linux/amd64 and linux/arm64.
#   docker build -t food-assistant .
#   docker buildx build --platform linux/amd64,linux/arm64 -t food-assistant .
#
# The build stage always runs on the build machine's own architecture and publishes portable,
# framework-dependent IL. That output runs on every target architecture, so multi-arch builds
# need no emulation (QEMU) for the slow compile step.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, in its own layer: it only reruns when project files or package versions change.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/FoodAssistant.Application/FoodAssistant.Application.csproj src/FoodAssistant.Application/
COPY src/FoodAssistant.Client/FoodAssistant.Client.csproj src/FoodAssistant.Client/
COPY src/FoodAssistant.Contracts/FoodAssistant.Contracts.csproj src/FoodAssistant.Contracts/
COPY src/FoodAssistant.Domain/FoodAssistant.Domain.csproj src/FoodAssistant.Domain/
COPY src/FoodAssistant.Infrastructure/FoodAssistant.Infrastructure.csproj src/FoodAssistant.Infrastructure/
COPY src/FoodAssistant.Server/FoodAssistant.Server.csproj src/FoodAssistant.Server/
COPY src/FoodAssistant.UI/FoodAssistant.UI.csproj src/FoodAssistant.UI/
COPY src/FoodAssistant.Web.Client/FoodAssistant.Web.Client.csproj src/FoodAssistant.Web.Client/
RUN dotnet restore src/FoodAssistant.Server/FoodAssistant.Server.csproj

COPY src/ src/
# No --no-restore: the Web SDK adds the package with _framework/blazor.web.js only when it sees
# the Razor components, which are copied after the cached restore above (found in M1 step 0).
RUN dotnet publish src/FoodAssistant.Server/FoodAssistant.Server.csproj \
        --configuration Release --output /app/publish -p:UseAppHost=false

# Writable directories for the volumes, owned by the non-root app user (UID 1654 in the .NET images).
# Created here because COPY --chown needs no RUN (and thus no emulation) in the final stage.
RUN mkdir -p /volumes/home/app/.aspnet/DataProtection-Keys /volumes/backups

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build --chown=1654:1654 /volumes/home/app/.aspnet /home/app/.aspnet
COPY --from=build --chown=1654:1654 /volumes/backups /backups
COPY --from=build /app/publish .

# Plain HTTP inside the container; TLS ends at the reverse proxy (Caddy, Tailscale, ...).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER 1654
ENTRYPOINT ["dotnet", "FoodAssistant.Server.dll"]
