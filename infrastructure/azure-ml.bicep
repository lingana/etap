/**
 * Azure ML Integration Bicep Template
 * Deploys Machine Learning Service and inference endpoint
 */

param location string = resourceGroup().location
param environment string = 'dev'
param workspaceName string = 'excise-ml-${environment}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'excisemlstore${environment}${uniqueString(resourceGroup().id)}'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
  }
  tags: {
    environment: environment
  }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: 'excisemlreg${environment}${uniqueString(resourceGroup().id)}'
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    environment: environment
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: 'exciseml-kv-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
  }
  tags: {
    environment: environment
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'exciseml-insights-${environment}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 30
  }
  tags: {
    environment: environment
  }
}

resource mlWorkspace 'Microsoft.MachineLearningServices/workspaces@2023-04-01' = {
  name: workspaceName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    friendlyName: 'Excise Tax ML Workspace'
    description: 'Machine Learning workspace for fuel transaction anomaly detection'
    storageAccount: storageAccount.id
    containerRegistry: containerRegistry.id
    keyVault: keyVault.id
    applicationInsights: applicationInsights.id
  }
  tags: {
    environment: environment
    purpose: 'AnomalyDetection'
  }
}

@export()
output mlWorkspaceId string = mlWorkspace.id
@export()
output mlWorkspaceName string = mlWorkspace.name
@export()
output mlWorkspaceResourceGroup string = resourceGroup().name
@export()
output mlWorkspaceSubscriptionId string = subscription().subscriptionId
