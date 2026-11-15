// Key Vault with the secrets the apps read. Secret names use "--", which .NET reads as ":" (Jwt--Key is Jwt:Key).
param location string
param name string
param identityPrincipalId string

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

// Built-in role "Key Vault Secrets User": read secret values, nothing else.
var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

resource appsCanReadSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, identityPrincipalId, secretsUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
    principalId: identityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource secretJwtKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Jwt--Key'
  properties: {
    value: jwtKey
  }
}

resource secretIngestKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Ingest--ApiKey'
  properties: {
    value: ingestApiKey
  }
}

resource secretAdminPassword 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Admin--Password'
  properties: {
    value: adminPassword
  }
}

resource secretDatabaseUrl 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'DATABASE-URL'
  properties: {
    value: databaseUrl
  }
}

resource secretDatabaseUrlUnpooled 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'DATABASE-URL-UNPOOLED'
  properties: {
    value: databaseUrlUnpooled
  }
}

output vaultUri string = vault.properties.vaultUri
