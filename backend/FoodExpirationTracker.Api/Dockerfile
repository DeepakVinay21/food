FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY . .
RUN dotnet restore FoodExpirationTracker.Api/FoodExpirationTracker.Api.csproj
RUN dotnet publish FoodExpirationTracker.Api/FoodExpirationTracker.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FoodExpirationTracker.Api.dll"]
