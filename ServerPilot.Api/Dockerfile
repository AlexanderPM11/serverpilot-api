# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + project files first (layer caching)
COPY ServerPilot.slnx                                      ./
COPY ServerPilot.Domain/ServerPilot.Domain.csproj          ServerPilot.Domain/
COPY ServerPilot.Application/ServerPilot.Application.csproj ServerPilot.Application/
COPY ServerPilot.Infrastructure/ServerPilot.Infrastructure.csproj ServerPilot.Infrastructure/
COPY ServerPilot.Api/ServerPilot.Api.csproj                ServerPilot.Api/

RUN dotnet restore ServerPilot.Api/ServerPilot.Api.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish ServerPilot.Api/ServerPilot.Api.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create a non-root user for security
RUN addgroup --system --gid 1001 appgroup \
 && adduser  --system --uid 1001 --ingroup appgroup appuser

COPY --from=build /app/publish .

# SQLite DB will live in a volume at /app/data
RUN mkdir -p /app/data && chown appuser:appgroup /app/data
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/serverpilot.db"

USER appuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ServerPilot.Api.dll"]
