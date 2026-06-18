FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/Zootact.Core/Zootact.Core.csproj backend/Zootact.Core/
COPY backend/Zootact.Infrastructure/Zootact.Infrastructure.csproj backend/Zootact.Infrastructure/
COPY backend/Zootact.API/Zootact.API.csproj backend/Zootact.API/

RUN dotnet restore backend/Zootact.API/Zootact.API.csproj

COPY backend/ backend/

RUN dotnet publish backend/Zootact.API/Zootact.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

RUN groupadd --system zootact \
    && useradd --system --gid zootact --no-create-home --shell /usr/sbin/nologin zootact
USER zootact

ENTRYPOINT ["dotnet", "Zootact.API.dll"]
