// Everything inside the resource group: logs, Container Apps environment, identity, Key Vault, the two apps (Api and Web).
param location string
param appName string

param apiImage string
param webImage string
param registryServer string
param registryUsername string
@secure()
param registryPassword string

param adminEmail string
@secure()
param jwtKey string
@secure()
param ingestApiKey string
@secure()
param adminPassword string
@secure()
param databaseUrl string
@secure()
param databaseUrlUnpooled string

param webMinReplicas int
param apiExternalIngress bool

var keyVaultName = take('${appName}-kv-${uniqueString(resourceGroup().id)}', 24)

resource logs 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${appName}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${appName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// One user-assigned identity shared by the apps. A system-assigned identity cannot be used here: it only
// exists after the app is created, but the app needs Key Vault access to be created (secret references).
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${appName}-id'
  location: location
}

module keyvault 'keyvault.bicep' = {
  name: 'pakar-keyvault'
  params: {
    location: location
    name: keyVaultName
    identityPrincipalId: identity.properties.principalId
    jwtKey: jwtKey
    ingestApiKey: ingestApiKey
    adminPassword: adminPassword
    databaseUrl: databaseUrl
    databaseUrlUnpooled: databaseUrlUnpooled
  }
}

module apps 'containerapps.bicep' = {
  name: 'pakar-apps'
  // Using keyvault.outputs below makes this wait until the whole Key Vault module is done,
  // including the role assignment and the secrets that the apps read when they start.
  params: {
    location: location
    appName: appName
    environmentId: env.id
    environmentDefaultDomain: env.properties.defaultDomain
    identityId: identity.id
    vaultUri: keyvault.outputs.vaultUri
    apiImage: apiImage
    webImage: webImage
    registryServer: registryServer
    registryUsername: registryUsername
    registryPassword: registryPassword
    adminEmail: adminEmail
    webMinReplicas: webMinReplicas
    apiExternalIngress: apiExternalIngress
  }
}

output webUrl string = apps.outputs.webUrl
output apiUrl string = apps.outputs.apiUrl
output keyVaultName string = keyVaultName
