# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/WordSearch.Api/WordSearch.Api.csproj src/WordSearch.Api/
RUN dotnet restore src/WordSearch.Api/WordSearch.Api.csproj

COPY src/ src/
RUN dotnet publish src/WordSearch.Api/WordSearch.Api.csproj \
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
ENTRYPOINT ["dotnet", "WordSearch.Api.dll"]
