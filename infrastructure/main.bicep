param location string = 'eastus'
param environment string = 'dev'
param projectName string = 'excise-tax-audit'

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-06-01' = {
  name: '${projectName}storage${environment}'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
  }
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-06-01' = {
  name: '${storageAccount.name}/default/uploads'
  properties: {
    publicAccess: 'None'
  }
}

// SQL Database
resource sqlServer 'Microsoft.Sql/servers@2021-02-01' = {
  name: '${projectName}-server-${environment}'
  location: location
  properties: {
    administratorLogin: 'sqladmin'
    administratorLoginPassword: 'ChangeMe@1234!' // TODO: Use Azure Key Vault
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2021-02-01' = {
  parent: sqlServer
  name: 'ExciseTaxAudit'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2021-02-01' = {
  name: '${projectName}-plan-${environment}'
  location: location
  sku: {
    name: 'B2'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// Web App (Backend API)
resource webApp 'Microsoft.Web/sites@2021-02-01' = {
  name: '${projectName}-api-${environment}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${projectName}-insights-${environment}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

// Output
output storageAccountName string = storageAccount.name
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output webAppName string = webApp.name
output appInsightsKey string = appInsights.properties.InstrumentationKey
