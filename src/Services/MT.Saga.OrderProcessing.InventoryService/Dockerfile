# syntax=docker/dockerfile:1.7

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-alpine
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0-alpine

FROM ${SDK_IMAGE} AS restore
WORKDIR /src

# Copy build/CPM metadata first for better restore cache reuse
COPY global.json ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY Directory.Packages.props ./

# Copy project files
COPY src/MT.Saga.OrderProcessing.Contracts/MT.Saga.OrderProcessing.Contracts.csproj src/MT.Saga.OrderProcessing.Contracts/
COPY src/Services/MT.Saga.OrderProcessing.InventoryService/MT.Saga.OrderProcessing.InventoryService.csproj src/Services/MT.Saga.OrderProcessing.InventoryService/

RUN dotnet restore src/Services/MT.Saga.OrderProcessing.InventoryService/MT.Saga.OrderProcessing.InventoryService.csproj

FROM restore AS publish
ARG BUILD_CONFIGURATION=Release

# Copy only source required by this service graph
COPY src/MT.Saga.OrderProcessing.Contracts/ src/MT.Saga.OrderProcessing.Contracts/
COPY src/Services/MT.Saga.OrderProcessing.InventoryService/ src/Services/MT.Saga.OrderProcessing.InventoryService/

RUN dotnet publish src/Services/MT.Saga.OrderProcessing.InventoryService/MT.Saga.OrderProcessing.InventoryService.csproj \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    --no-restore

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MT.Saga.OrderProcessing.InventoryService.dll"]
