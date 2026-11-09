targetScope = 'resourceGroup'

@description('Prefixo curto usado nos nomes dos recursos. Deve ser único no resource group.')
@minLength(3)
@maxLength(20)
param namePrefix string = 'estac-hml'

@description('Região dos recursos de computação e banco (App Service e Azure SQL).')
param location string = 'brazilsouth'

@description('Região do Azure Static Web Apps. Homologação usa uma região suportada pelo serviço.')
param swaLocation string = 'eastus2'

@description('Login administrativo inicial do Azure SQL. Usado apenas no provisionamento. Evite ponto e vírgula e igual, pois a connection string é montada por interpolação.')
@minLength(1)
@maxLength(128)
param sqlAdminLogin string

@description('Senha administrativa inicial do Azure SQL. Não é versionada. Evite ponto e vírgula e aspas, pois a connection string é montada por interpolação.')
@secure()
@minLength(12)
param sqlAdminPassword string

@description('Senha da conta sintética de manobrista, usada somente no bootstrap de homologação.')
@secure()
@minLength(12)
param valetPassword string

@description('Senha da conta sintética de caixa, usada somente no bootstrap de homologação.')
@secure()
@minLength(12)
param cashierPassword string

@description('Senha da conta sintética de administrador, usada somente no bootstrap de homologação.')
@secure()
@minLength(12)
param adminPassword string

@description('IP público do executor, opcional, para permitir o bootstrap local no Azure SQL. Informe somente o IP, sem máscara.')
param deployClientIp string = ''

@description('Nome do banco operacional.')
param databaseName string = 'Estacionamento'

var suffix = uniqueString(resourceGroup().id)
var apiName = toLower('${take(namePrefix, 20)}-api-${suffix}')
var planName = toLower('${take(namePrefix, 20)}-plan')
var spaName = toLower('${take(namePrefix, 20)}-spa-${suffix}')
var sqlName = toLower('${take(namePrefix, 20)}-sql-${suffix}')
var vaultName = toLower('${take(namePrefix, 6)}kv${suffix}')
var sqlConnectionString = 'Server=tcp:${sql.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

resource spa 'Microsoft.Web/staticSites@2025-03-01' = {
  name: spaName
  location: swaLocation
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {}
}

resource plan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource api 'Microsoft.Web/sites@2024-11-01' = {
  name: apiName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'ConnectionStrings__Identity'
          value: '@Microsoft.KeyVault(SecretUri=${vault.properties.vaultUri}secrets/SqlConnectionString/)'
        }
        {
          name: 'DataProtection__KeyRingPath'
          value: '/home/.dataprotection'
        }
        {
          name: 'Security__SpaOrigin'
          value: 'https://${spa.properties.defaultHostname}'
        }
        {
          name: 'Authentication__IdleTimeoutSeconds'
          value: '1200'
        }
        {
          name: 'Authentication__LoginPermitLimit'
          value: '5'
        }
      ]
    }
  }
}

resource sql 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sql
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sql
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource allowDeployClient 'Microsoft.Sql/servers/firewallRules@2023-08-01' = if (!empty(deployClientIp)) {
  parent: sql
  name: 'AllowDeployClient'
  properties: {
    startIpAddress: deployClientIp
    endIpAddress: deployClientIp
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'SqlConnectionString'
  properties: {
    value: sqlConnectionString
  }
}

resource valetSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'SyntheticValetPassword'
  properties: {
    value: valetPassword
  }
}

resource cashierSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'SyntheticCashierPassword'
  properties: {
    value: cashierPassword
  }
}

resource adminSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'SyntheticAdminPassword'
  properties: {
    value: adminPassword
  }
}

resource apiSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, api.id, 'secrets-user')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource spaLinkedApi 'Microsoft.Web/staticSites/linkedBackends@2025-03-01' = {
  parent: spa
  name: uniqueString(api.id)
  properties: {
    backendResourceId: api.id
    region: location
  }
}

@description('Nome do resource group usado no deploy.')
output resourceGroupName string = resourceGroup().name

@description('Nome do App Service da API.')
output apiAppName string = api.name

@description('Hostname padrão da API (não deve ser exposto ao browser).')
output apiDefaultHostName string = api.properties.defaultHostName

@description('URL HTTPS do App Service da API.')
output apiUrl string = 'https://${api.properties.defaultHostName}'

@description('Nome do Azure Static Web Apps.')
output spaName string = spa.name

@description('URL pública da SPA em homologação.')
output spaUrl string = 'https://${spa.properties.defaultHostname}'

@description('Nome do Azure SQL.')
output sqlServerName string = sql.name

@description('FQDN do Azure SQL.')
output sqlServerFqdn string = sql.properties.fullyQualifiedDomainName

@description('Nome do banco operacional.')
output databaseName string = database.name

@description('Nome do Key Vault.')
output keyVaultName string = vault.name

@description('URI do Key Vault.')
output keyVaultUri string = vault.properties.vaultUri
