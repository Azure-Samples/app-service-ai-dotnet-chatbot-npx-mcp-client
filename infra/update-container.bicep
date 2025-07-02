targetScope = 'resourceGroup'

@description('Name of the App Service to update')
param appServiceName string

@description('Container registry login server')
param containerRegistryLoginServer string

@description('Container image name with tag')
param containerImageName string = 'aspnet-npx:latest'

// Reference the existing App Service
resource appService 'Microsoft.Web/sites@2024-11-01' existing = {
  name: appServiceName
}

// Update the App Service configuration
resource appServiceConfig 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: appService
  name: 'web'
  properties: {
    linuxFxVersion: 'DOCKER|${containerRegistryLoginServer}/${containerImageName}'
    acrUseManagedIdentityCreds: true
  }
}
