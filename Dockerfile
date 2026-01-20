# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY PingPay.sln .
COPY src/PingPay.Api/PingPay.Api.csproj src/PingPay.Api/
COPY src/PingPay.Core/PingPay.Core.csproj src/PingPay.Core/
COPY src/PingPay.Infrastructure/PingPay.Infrastructure.csproj src/PingPay.Infrastructure/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ src/

# Build and publish
WORKDIR /src/src/PingPay.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "PingPay.Api.dll"]
