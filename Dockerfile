# ============================================
# Stage 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for better caching
COPY BE-PRM393-FASHIONSTYLE.sln .
COPY MV.DomainLayer/MV.DomainLayer.csproj MV.DomainLayer/
COPY MV.ApplicationLayer/MV.ApplicationLayer.csproj MV.ApplicationLayer/
COPY MV.InfrastructureLayer/MV.InfrastructureLayer.csproj MV.InfrastructureLayer/
COPY MV.PresentationLayer/MV.PresentationLayer.csproj MV.PresentationLayer/

# Restore packages
RUN dotnet restore

# Copy all source code
COPY . .

# Build and publish
RUN dotnet publish MV.PresentationLayer/MV.PresentationLayer.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================================
# Stage 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Expose port (Render uses PORT env var)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV EnableSwagger=true

# Copy published output
COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/swagger/index.html || exit 1

ENTRYPOINT ["dotnet", "MV.PresentationLayer.dll"]
