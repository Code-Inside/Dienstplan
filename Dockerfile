FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY Dienstplan.Web/Dienstplan.Web.csproj Dienstplan.Web/
RUN dotnet restore Dienstplan.Web/Dienstplan.Web.csproj
COPY Dienstplan.Web/ Dienstplan.Web/
RUN dotnet publish Dienstplan.Web/Dienstplan.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Azure Container Apps terminates TLS at the ingress; the container itself listens on plain HTTP 8080.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Dienstplan.Web.dll"]
