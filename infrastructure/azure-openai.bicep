/**
 * Azure OpenAI Resource Deployment
 * Deploys Azure OpenAI service with gpt-3.5-turbo deployment
 * Part of Excise Tax Audit Platform infrastructure
 */

param location string = resourceGroup().location
param environment string = 'dev' // dev, staging, prod
param openAISkuName string = 'S0' // Standard tier
param deploymentName string = 'gpt-35-turbo'
param modelName string = 'gpt-3.5-turbo'
param modelVersion string = '0613'
param capacity int = 10 // TPM capacity

@description('Azure OpenAI resource')
resource openAIResource 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: 'excise-openai-${environment}-${uniqueString(resourceGroup().id)}'
  location: location
  kind: 'OpenAI'
  sku: {
    name: openAISkuName
  }
  properties: {
    apiProperties: {
      statisticsEnabled: true
    }
    customSubdomainName: 'excise-openai-${environment}-${uniqueString(resourceGroup().id)}'
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    environment: environment
    purpose: 'GenAI-Anomaly-Explanations'
    project: 'ExciseTaxAudit'
  }
}

@description('GPT-3.5-turbo model deployment')
resource gpt35deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAIResource
  name: deploymentName
  sku: {
    name: 'Standard'
    capacity: capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
    scaleSettings: {
      scaleType: 'Standard'
    }
  }
}

@description('Key Vault for storing OpenAI credentials')
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: 'excise-kv-${environment}-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
  }
  tags: {
    environment: environment
    purpose: 'SecretsManagement'
  }
}

@description('Store OpenAI API Key in Key Vault')
resource openAIKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'azure-openai-key'
  properties: {
    value: openAIResource.listKeys().key1
  }
}

@description('Store OpenAI Endpoint in Key Vault')
resource openAIEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'azure-openai-endpoint'
  properties: {
    value: openAIResource.properties.endpoint
  }
}

@description('Application Insights for monitoring')
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'excise-insights-${environment}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    RetentionInDays: 30
  }
  tags: {
    environment: environment
    purpose: 'Monitoring'
  }
}

@description('Log Analytics workspace for detailed logging')
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2021-12-01-preview' = {
  name: 'excise-logs-${environment}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
  tags: {
    environment: environment
  }
}

@description('Diagnostic settings for OpenAI')
resource openAIDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'openai-diagnostics'
  scope: openAIResource
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      {
        category: 'RequestResponse'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
      {
        category: 'AuditEvent'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
    ]
  }
}

@export()
output openAIEndpoint string = openAIResource.properties.endpoint
@export()
output openAIResourceId string = openAIResource.id
@export()
output openAIResourceName string = openAIResource.name
@export()
output deploymentName string = gpt35deployment.name
@export()
output keyVaultName string = keyVault.name
@export()
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
@export()
output logAnalyticsWorkspaceId string = logAnalytics.id
