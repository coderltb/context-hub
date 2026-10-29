# Restore project metadata separately so source changes reuse the dependency layer.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /source
COPY ["src/services/Authentication/ContextHub.Authentication.Api/ContextHub.Authentication.Api.csproj", "src/services/Authentication/ContextHub.Authentication.Api/"]
COPY ["src/services/Authentication/ContextHub.Authentication.Application/ContextHub.Authentication.Application.csproj", "src/services/Authentication/ContextHub.Authentication.Application/"]
COPY ["src/services/Authentication/ContextHub.Authentication.Domain/ContextHub.Authentication.Domain.csproj", "src/services/Authentication/ContextHub.Authentication.Domain/"]
COPY ["src/services/Authentication/ContextHub.Authentication.Infrastructure/ContextHub.Authentication.Infrastructure.csproj", "src/services/Authentication/ContextHub.Authentication.Infrastructure/"]
COPY ["src/shared/ContextHub.Shared/ContextHub.Shared.csproj", "src/shared/ContextHub.Shared/"]
COPY ["src/shared/ContextHub.Core/ContextHub.Core.csproj", "src/shared/ContextHub.Core/"]
COPY ["src/shared/ContextHub.Cache/ContextHub.Cache.csproj", "src/shared/ContextHub.Cache/"]
COPY ["src/shared/ContextHub.ApiCommon/ContextHub.ApiCommon.csproj", "src/shared/ContextHub.ApiCommon/"]
COPY ["src/shared/ContextHub.Observability/ContextHub.Observability.csproj", "src/shared/ContextHub.Observability/"]
RUN dotnet restore src/services/Authentication/ContextHub.Authentication.Api/ContextHub.Authentication.Api.csproj
COPY src/services/Authentication/ src/services/Authentication/
COPY src/shared/ src/shared/
RUN dotnet publish src/services/Authentication/ContextHub.Authentication.Api/ContextHub.Authentication.Api.csproj --no-restore -c Release -o /out /p:UseAppHost=false

# The runtime receives published files only; secrets are supplied when the container starts.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    HOME=/home/app
# A persistent volume at this native location preserves cookies and invitation proofs across replacement.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
COPY --from=build --chown=app:app /out/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ContextHub.Authentication.Api.dll"]
