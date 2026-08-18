# Build aşaması: çözümü derle ve publish et.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Katman önbelleği için önce build props + proje dosyaları (yalnızca yayınlanan API zinciri;
# test projesi imaja girmez).
COPY Directory.Build.props .
COPY src/Accounting.Domain/Accounting.Domain.csproj src/Accounting.Domain/
COPY src/Accounting.Contracts/Accounting.Contracts.csproj src/Accounting.Contracts/
COPY src/Accounting.Application/Accounting.Application.csproj src/Accounting.Application/
COPY src/Accounting.Infrastructure/Accounting.Infrastructure.csproj src/Accounting.Infrastructure/
COPY src/Accounting.Api/Accounting.Api.csproj src/Accounting.Api/
RUN dotnet restore src/Accounting.Api/Accounting.Api.csproj

COPY . .
RUN dotnet publish src/Accounting.Api/Accounting.Api.csproj -c Release -o /app/publish --no-restore

# Runtime aşaması: yalnızca publish çıktısı.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Healthcheck için curl.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Root olarak çalıştırma.
USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "Accounting.Api.dll"]
