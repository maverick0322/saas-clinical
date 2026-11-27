FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Clinical.Domain/Clinical.Domain.csproj src/Clinical.Domain/
COPY src/Clinical.Application/Clinical.Application.csproj src/Clinical.Application/
COPY src/Clinical.Infrastructure/Clinical.Infrastructure.csproj src/Clinical.Infrastructure/
COPY src/Clinical.Api/Clinical.Api.csproj src/Clinical.Api/
RUN dotnet restore src/Clinical.Api/Clinical.Api.csproj

COPY src/Clinical.Domain/ src/Clinical.Domain/
COPY src/Clinical.Application/ src/Clinical.Application/
COPY src/Clinical.Infrastructure/ src/Clinical.Infrastructure/
COPY src/Clinical.Api/ src/Clinical.Api/
RUN dotnet publish src/Clinical.Api/Clinical.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "Clinical.Api.dll"]
