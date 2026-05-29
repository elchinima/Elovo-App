FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Elovo.NET/Elovo.Domain/Elovo.Domain.csproj Elovo.NET/Elovo.Domain/
COPY Elovo.NET/Elovo.Application/Elovo.Application.csproj Elovo.NET/Elovo.Application/
COPY Elovo.NET/Elovo.Infrastructure/Elovo.Infrastructure.csproj Elovo.NET/Elovo.Infrastructure/
COPY Elovo.NET/Elovo.Web/Elovo.Web.csproj Elovo.NET/Elovo.Web/

RUN dotnet restore Elovo.NET/Elovo.Web/Elovo.Web.csproj

COPY Elovo.NET/ Elovo.NET/
RUN dotnet publish Elovo.NET/Elovo.Web/Elovo.Web.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["sh", "-c", "dotnet Elovo.Web.dll --urls http://0.0.0.0:${PORT:-8080}"]
