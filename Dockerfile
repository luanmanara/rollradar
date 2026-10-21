# API image for Render (built from the repository root). Render routes traffic to port 10000 by default.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish backend/RollRadar.Api -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=10000 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_gcServer=0
USER app
EXPOSE 10000
ENTRYPOINT ["dotnet", "RollRadar.Api.dll"]
