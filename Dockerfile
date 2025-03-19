# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . ./src
RUN dotnet build "./Schnauzer/Schnauzer.csproj" -c Release -o /build 
RUN dotnet publish "./Schnauzer.Core/Schnauzer.Core.csproj" -c Release -o /publish
 
# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
WORKDIR /publish
ENTRYPOINT ["dotnet", "Schnauzer.dll"]
