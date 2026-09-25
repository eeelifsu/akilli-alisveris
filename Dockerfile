# Akıllı Alışveriş: .NET 8 API + web sitesi tek imajda.
# Derleme:  docker build -t akilli-alisveris .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/ShoppingAssistant.Api/ShoppingAssistant.Api.csproj backend/ShoppingAssistant.Api/
RUN dotnet restore backend/ShoppingAssistant.Api/ShoppingAssistant.Api.csproj
COPY backend/ backend/
RUN dotnet publish backend/ShoppingAssistant.Api/ShoppingAssistant.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
COPY web ./web
ENV ASPNETCORE_ENVIRONMENT=Production \
    WebRoot=/app/web \
    AUTO_MIGRATE=true
# Render PORT değişkenini verir; yoksa 8080.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet ShoppingAssistant.Api.dll"]
