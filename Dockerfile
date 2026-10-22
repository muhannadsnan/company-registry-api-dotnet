# Build stage: full SDK, restore first so the package layer is cached between builds.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/CompanyRegistry.Api/CompanyRegistry.Api.csproj src/CompanyRegistry.Api/
RUN dotnet restore src/CompanyRegistry.Api/CompanyRegistry.Api.csproj
COPY src/ src/
RUN dotnet publish src/CompanyRegistry.Api --configuration Release --no-restore --output /app

# Runtime stage: only the ASP.NET runtime and the published app, running as a non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CompanyRegistry.Api.dll"]
