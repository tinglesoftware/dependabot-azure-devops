#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0-bullseye-slim AS base
LABEL org.opencontainers.image.source="https://github.com/tinglesoftware/dependabot-azure-devops"
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build
WORKDIR /src
COPY ["Tingle.Dependabot/Tingle.Dependabot.csproj", "Tingle.Dependabot/"]
RUN dotnet restore "Tingle.Dependabot/Tingle.Dependabot.csproj"
COPY . .
WORKDIR "/src/Tingle.Dependabot"
RUN dotnet build "Tingle.Dependabot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Tingle.Dependabot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tingle.Dependabot.dll"]