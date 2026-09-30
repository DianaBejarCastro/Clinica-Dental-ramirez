FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY apps/api/ClinicaRamirez.Api.csproj apps/api/packages.lock.json apps/api/
RUN dotnet restore apps/api/ClinicaRamirez.Api.csproj --locked-mode
COPY apps/api apps/api
RUN dotnet publish apps/api/ClinicaRamirez.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClinicaRamirez.Api.dll"]
