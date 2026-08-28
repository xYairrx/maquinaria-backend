FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Maquinaria.Api/Maquinaria.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Railway inyecta PORT; 8080 es el de la imagen base para correr en local.
ENTRYPOINT ["sh", "-c", "exec dotnet Maquinaria.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
