FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /source
COPY . .
RUN dotnet publish "$PROJECT" -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
ARG ASSEMBLY
ENV APP_ASSEMBLY=$ASSEMBLY ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /auth-keys && chown "$APP_UID:$APP_UID" /auth-keys && chmod 700 /auth-keys
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=12 CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_ASSEMBLY.dll\""]
