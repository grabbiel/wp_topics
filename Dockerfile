# The project sits at the repository root, so the build context root is also
# the project directory. Both names are spelled out rather than inferred:
# a wrong path fails the build loudly, a wrong assembly name fails at
# container start with a much less obvious error.

# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project file alone, so the NuGet layer is reused
# whenever only source changed.
COPY WordSearch.Api.csproj .
RUN dotnet restore WordSearch.Api.csproj

COPY . .

# The project must be named explicitly. WordSearch.slnx sits next to the
# .csproj, and MSBuild refuses to guess when a directory holds both a
# solution and a project (MSB1011).
RUN dotnet publish WordSearch.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# Run
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app .

# Container Apps routes to this port and runs the probes against it.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID

# Exec form, so the app is PID 1 and gets SIGTERM when a revision drains.
# The chiseled image has no shell, so this cannot be a shell command.
ENTRYPOINT ["dotnet", "WordSearch.Api.dll"]