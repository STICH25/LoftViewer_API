# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY global.json Directory.Build.props ./
COPY src/LoftViewer/LoftViewer.csproj src/LoftViewer/
RUN dotnet restore src/LoftViewer/LoftViewer.csproj

COPY src/ src/
RUN dotnet publish src/LoftViewer/LoftViewer.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
# The official images ship a non-root "app" user; never run the API as root.
USER $APP_UID
ENTRYPOINT ["dotnet", "LoftViewer.dll"]
