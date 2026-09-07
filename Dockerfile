FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RemoteDesktop.Server.csproj", "./"]
RUN dotnet restore "./RemoteDesktop.Server.csproj"
COPY . .
RUN dotnet publish "RemoteDesktop.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RemoteDesktop.Server.dll"]