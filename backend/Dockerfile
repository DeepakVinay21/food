FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet restore FoodExpirationTracker.Api/FoodExpirationTracker.Api.csproj
RUN dotnet publish FoodExpirationTracker.Api -c Release -o /app /p:PublishTrimmed=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
RUN apk add --no-cache icu-libs
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_GCHeapHardLimit=0x10000000
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 10000
ENTRYPOINT ["dotnet", "FoodExpirationTracker.Api.dll"]
