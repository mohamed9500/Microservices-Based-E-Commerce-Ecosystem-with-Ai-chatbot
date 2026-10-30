# Generic build for every .NET service. Build context = repository root.
#   docker build -f infrastructure/docker/dotnet.Dockerfile --build-arg PROJECT=services/identity/Nexus.Identity.csproj --build-arg ASSEMBLY=Nexus.Identity -t nexus-identity .
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
ARG PROJECT
ARG ASSEMBLY
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY contracts/ contracts/
COPY building-blocks/ building-blocks/
COPY gateway/ gateway/
COPY services/ services/
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
ARG ASSEMBLY
ENV ASSEMBLY=$ASSEMBLY
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV Urls=http://+:8080 ASPNETCORE_URLS=http://+:8080 DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER 1654
HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=8 CMD curl -fsS http://localhost:8080/health/live || exit 1
ENTRYPOINT ["sh", "-c", "exec dotnet /app/${ASSEMBLY}.dll"]
