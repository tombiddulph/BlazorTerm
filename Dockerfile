FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SOURCE_REVISION=development
ARG BUILD_TIMESTAMP=development
WORKDIR /src

COPY BlazorTerm.csproj ./
RUN dotnet restore BlazorTerm.csproj

COPY . ./
RUN dotnet publish BlazorTerm.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:UseAppHost=false \
    -p:InformationalVersion="1.0.0+${SOURCE_REVISION}"

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG SOURCE_REVISION=development
ARG BUILD_TIMESTAMP=development
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV BLAZORTERM_GIT_SHA=$SOURCE_REVISION
ENV BLAZORTERM_BUILD_TIMESTAMP=$BUILD_TIMESTAMP
EXPOSE 8080

COPY --from=build /app/publish ./

USER $APP_UID
ENTRYPOINT ["dotnet", "BlazorTerm.dll"]
