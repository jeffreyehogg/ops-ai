# Multi-Stage Production Dockerfile for OpsAI

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project definitions for optimized layer caching
COPY OpsAI.sln Directory.Build.props ./
COPY src/OpsAI.Core/OpsAI.Core.csproj src/OpsAI.Core/
COPY src/OpsAI.Infrastructure/OpsAI.Infrastructure.csproj src/OpsAI.Infrastructure/
COPY src/OpsAI.Api/OpsAI.Api.csproj src/OpsAI.Api/
COPY tests/OpsAI.Tests/OpsAI.Tests.csproj tests/OpsAI.Tests/

RUN dotnet restore OpsAI.sln

# Copy remaining source code
COPY src/ src/
COPY tests/ tests/

# Run automated tests inside the build container
RUN dotnet test tests/OpsAI.Tests/OpsAI.Tests.csproj -c Release --no-restore

# Publish the API application
RUN dotnet publish src/OpsAI.Api/OpsAI.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Minimal Distroless / Hardened Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Non-root security user
USER app

COPY --from=build --chown=app:app /app/publish .

# Standard cloud port for Render, Google Cloud Run, and Azure Container Apps
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OpsAI.Api.dll"]
