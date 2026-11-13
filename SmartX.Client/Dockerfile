# Smart-X Blazor WASM — publish static files, serve with nginx
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SmartX.Shared/SmartX.Shared.csproj SmartX.Shared/
COPY SmartX.Client/SmartX.Client.csproj SmartX.Client/
RUN dotnet restore SmartX.Client/SmartX.Client.csproj

COPY SmartX.Shared/ SmartX.Shared/
COPY SmartX.Client/ SmartX.Client/
RUN dotnet publish SmartX.Client/SmartX.Client.csproj -c Release -o /app/publish --no-restore

FROM nginx:1.27-alpine AS final
COPY SmartX.Client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
