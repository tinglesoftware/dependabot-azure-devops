#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS base
LABEL org.opencontainers.image.source="https://github.com/tinglesoftware/dependabot-azure-devops"
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["server/Directory.Build.props", "server/"]
COPY ["server/Tingle.Dependabot/Tingle.Dependabot.csproj", "server/Tingle.Dependabot/"]
RUN dotnet restore "./server/Tingle.Dependabot/./Tingle.Dependabot.csproj"
COPY . .
WORKDIR "/src/server/Tingle.Dependabot"
RUN dotnet build "./Tingle.Dependabot.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Tingle.Dependabot.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tingle.Dependabot.dll"]