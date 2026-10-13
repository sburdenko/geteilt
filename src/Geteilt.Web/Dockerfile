# The browser client, served by the same Caddy that proxies /api to the API. One origin for both:
# no CORS policy exists in production at all, and the client calls the API on
# the address it was loaded from.
#
# Build context is the repository root, like the API's.

# Tailwind scans the Razor files for class names, so it runs on the sources, before publishing.
FROM node:22-alpine AS css
WORKDIR /source
COPY src/Geteilt.Web/package.json src/Geteilt.Web/package-lock.json src/Geteilt.Web/
RUN npm ci --prefix src/Geteilt.Web
COPY src/Geteilt.Web/ src/Geteilt.Web/
RUN npm run build:css --prefix src/Geteilt.Web

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Geteilt.Domain/Geteilt.Domain.csproj src/Geteilt.Domain/
COPY src/Geteilt.Contracts/Geteilt.Contracts.csproj src/Geteilt.Contracts/
COPY src/Geteilt.Web/Geteilt.Web.csproj src/Geteilt.Web/
RUN dotnet restore src/Geteilt.Web/Geteilt.Web.csproj

COPY src/Geteilt.Domain/ src/Geteilt.Domain/
COPY src/Geteilt.Contracts/ src/Geteilt.Contracts/
COPY src/Geteilt.Web/ src/Geteilt.Web/
COPY --from=css /source/src/Geteilt.Web/wwwroot/css/app.css src/Geteilt.Web/wwwroot/css/app.css
RUN dotnet publish src/Geteilt.Web/Geteilt.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM caddy:2.10-alpine AS final
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/wwwroot /srv
