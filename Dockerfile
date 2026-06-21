# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy project files first for layer-cached NuGet restore
COPY Logging/Logging.csproj Logging/
COPY LetterTemplatePractice/LetterTemplatePractice.csproj LetterTemplatePractice/
RUN dotnet restore LetterTemplatePractice/LetterTemplatePractice.csproj
# Copy all source
COPY Logging/ Logging/
COPY LetterTemplatePractice/ LetterTemplatePractice/
# Publish release build
RUN dotnet publish LetterTemplatePractice/LetterTemplatePractice.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Kerberos lib required by Npgsql's GSS encryption negotiation
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Logs directory (Serilog writes here)
RUN mkdir -p /app/Logs
COPY --from=build /app/publish .
# Render injects PORT at runtime; ASP.NET Core reads ASPNETCORE_URLS
ENV ASPNETCORE_ENVIRONMENT=Production
# Default port — Render overrides this with its own PORT value
EXPOSE 10000
# Use a shell entrypoint so $PORT is expanded at runtime
CMD ASPNETCORE_URLS="http://+:${PORT:-10000}" dotnet LetterTemplatePractice.dll
