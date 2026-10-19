# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props StockCart.sln ./
COPY src/StockCart.Domain/StockCart.Domain.csproj src/StockCart.Domain/
COPY src/StockCart.Application/StockCart.Application.csproj src/StockCart.Application/
COPY src/StockCart.Infrastructure/StockCart.Infrastructure.csproj src/StockCart.Infrastructure/
COPY src/StockCart.Api/StockCart.Api.csproj src/StockCart.Api/
RUN dotnet restore src/StockCart.Api/StockCart.Api.csproj
COPY src/ src/
RUN dotnet publish src/StockCart.Api/StockCart.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
# Run as the built-in non-root user
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "StockCart.Api.dll"]
