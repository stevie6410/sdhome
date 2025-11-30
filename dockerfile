# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY src/SDHome.Api/SDHome.Api.csproj SDHome.Api/
COPY src/SDHome.Lib/SDHome.Lib.csproj SDHome.Lib/

RUN dotnet restore SDHome.Api/SDHome.Api.csproj

# Copy source code
COPY src/ .

# Build and publish
RUN dotnet publish SDHome.Api/SDHome.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "SDHome.Api.dll"]
