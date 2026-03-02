// azure-sql.bicep
// Deploys Azure SQL Server and Database with AAD-only authentication
// Uses stable API version @2021-11-01

@description('Location for all resources')
param location string = 'uksouth'

@description('Azure AD Object ID of the administrator')
param adminObjectId string

@description('Azure AD User Principal Name (email) of the administrator')
param adminLogin string

@description('Principal ID of the managed identity for access')
param managedIdentityPrincipalId string

var sqlServerName = 'sql-${toLower(uniqueString(resourceGroup().id))}'

// Azure SQL Server with AAD-only authentication
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      principalType: 'User'
      sid: adminObjectId
      tenantId: subscription().tenantId
    }
  }
}

// Northwind database - Basic tier
resource database 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: 'Northwind'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// Firewall rule to allow Azure services
resource firewallRuleAzure 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// AAD-only authentication setting
resource aadOnlyAuth 'Microsoft.Sql/servers/azureADOnlyAuthentications@2021-11-01' = {
  parent: sqlServer
  name: 'Default'
  properties: {
    azureADOnlyAuthentication: true
  }
}

// Outputs
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName string = sqlServer.name
output databaseName string = database.name
