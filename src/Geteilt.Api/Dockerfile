# Build context is the repository root, so Directory.Build.props and Directory.Packages.props
# are visible — without them the container build would use different compiler settings than CI.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /source

# Project files first: this layer is cached until a dependency actually changes, so an ordinary
# code edit does not re-download the whole package graph.
# .editorconfig belongs here too: without it the image is compiled under different analyser
# rules than CI uses, and "builds on my machine, fails in Docker" follows.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Geteilt.Domain/Geteilt.Domain.csproj src/Geteilt.Domain/
COPY src/Geteilt.Contracts/Geteilt.Contracts.csproj src/Geteilt.Contracts/
COPY src/Geteilt.Application/Geteilt.Application.csproj src/Geteilt.Application/
COPY src/Geteilt.Infrastructure/Geteilt.Infrastructure.csproj src/Geteilt.Infrastructure/
COPY src/Geteilt.Api/Geteilt.Api.csproj src/Geteilt.Api/
RUN dotnet restore src/Geteilt.Api/Geteilt.Api.csproj

COPY src/ src/
RUN dotnet publish src/Geteilt.Api/Geteilt.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# The base image ships a non-root user; running as root inside a container buys nothing and
# turns a container escape into a host root escape.
USER $APP_UID

COPY --from=build --chown=$APP_UID /app .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "Geteilt.Api.dll"]
