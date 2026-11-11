# Build the CoffeeNChill application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY CoffeeNChill/CoffeeNChill.csproj CoffeeNChill/

RUN dotnet restore CoffeeNChill/CoffeeNChill.csproj

COPY CoffeeNChill/ CoffeeNChill/

RUN dotnet publish CoffeeNChill/CoffeeNChill.csproj -c Release -o /app/publish --no-restore


# Run using Azure Functions v4 with .NET 8
FROM mcr.microsoft.com/azure-functions/dotnet:4-dotnet8.0

ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV FUNCTIONS_WORKER_RUNTIME=dotnet
ENV FUNCTIONS_INPROC_NET8_ENABLED=1

COPY --from=build /app/publish /home/site/wwwroot
