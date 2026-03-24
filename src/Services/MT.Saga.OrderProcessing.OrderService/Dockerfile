# syntax=docker/dockerfile:1.7

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-alpine
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0-alpine

FROM ${SDK_IMAGE} AS restore
WORKDIR /src

# Copy build/CPM metadata first for better restore cache reuse
COPY global.json ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY Directory.Packages.props ./

# Copy project files
COPY src/MT.Saga.OrderProcessing.Contracts/MT.Saga.OrderProcessing.Contracts.csproj src/MT.Saga.OrderProcessing.Contracts/
COPY src/MT.Saga.OrderProcessing.Saga/MT.Saga.OrderProcessing.Saga.csproj src/MT.Saga.OrderProcessing.Saga/
COPY src/MT.Saga.OrderProcessing.Infrastructure/MT.Saga.OrderProcessing.Infrastructure.csproj src/MT.Saga.OrderProcessing.Infrastructure/
COPY src/Services/MT.Saga.OrderProcessing.OrderService/MT.Saga.OrderProcessing.OrderService.csproj src/Services/MT.Saga.OrderProcessing.OrderService/

RUN dotnet restore src/Services/MT.Saga.OrderProcessing.OrderService/MT.Saga.OrderProcessing.OrderService.csproj

FROM restore AS publish
ARG BUILD_CONFIGURATION=Release

# Copy only source required by this service graph
COPY src/MT.Saga.OrderProcessing.Contracts/ src/MT.Saga.OrderProcessing.Contracts/
COPY src/MT.Saga.OrderProcessing.Saga/ src/MT.Saga.OrderProcessing.Saga/
COPY src/MT.Saga.OrderProcessing.Infrastructure/ src/MT.Saga.OrderProcessing.Infrastructure/
COPY src/Services/MT.Saga.OrderProcessing.OrderService/ src/Services/MT.Saga.OrderProcessing.OrderService/

RUN dotnet publish src/Services/MT.Saga.OrderProcessing.OrderService/MT.Saga.OrderProcessing.OrderService.csproj \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    --no-restore

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app

# Use low ports for containerized Linux workloads
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MT.Saga.OrderProcessing.OrderService.dll"]
