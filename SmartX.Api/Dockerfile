# Smart-X API — multi-stage publish (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SmartX.Shared/SmartX.Shared.csproj SmartX.Shared/
COPY SmartX.Api/SmartX.Api.csproj SmartX.Api/
RUN dotnet restore SmartX.Api/SmartX.Api.csproj

COPY SmartX.Shared/ SmartX.Shared/
COPY SmartX.Api/ SmartX.Api/
RUN dotnet publish SmartX.Api/SmartX.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartX.Api.dll"]
