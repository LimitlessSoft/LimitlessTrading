FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["LT.Public.Api/LT.Public.Api.csproj", "LT.Public.Api/"]
RUN dotnet restore "LT.Public.Api/LT.Public.Api.csproj"
COPY . .
WORKDIR "/src/LT.Public.Api"
RUN dotnet build "LT.Public.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LT.Public.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LT.Public.Api.dll"]
