# ---------- Bauen ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Zuerst nur die Projektdatei kopieren: Die Paketwiederherstellung wird so zwischengespeichert.
COPY StockManager/StockManager.csproj StockManager/
RUN dotnet restore StockManager/StockManager.csproj

COPY StockManager/ StockManager/
RUN dotnet publish StockManager/StockManager.csproj -c Release -o /app/publish --no-restore

# ---------- Ausführen ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# Nicht als root ausführen (der Benutzer "app" ist in den .NET-Images eingebaut).
USER $APP_UID
ENTRYPOINT ["dotnet", "StockManager.dll"]