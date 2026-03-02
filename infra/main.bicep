// main.bicep
// Orchestrates deployment of App Service, Azure SQL, and optionally GenAI resources

@description('Location for all resources (except OpenAI which must be swedencentral)')
param location string = 'uksouth'

@description('Azure AD Object ID of the SQL administrator')
param adminObjectId string

@description('Azure AD User Principal Name (email) of the SQL administrator')
param adminLogin string

@description('Whether to deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

// App Service + Managed Identity module
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
  }
}

// Azure SQL module
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// GenAI module (conditional)
module genAI 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityName string = appService.outputs.managedIdentityName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn

// Null-safe GenAI outputs - only populated when deployGenAI=true
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
