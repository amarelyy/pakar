// PAKAR on Azure Container Apps (issue #28). Deploys at subscription scope so the resource group is created too.
//   az deployment sub create --location eastus2 --template-file infra/bicep/main.bicep \
//     --parameters @infra/main.parameters.example.json --parameters jwtKey=... (see infra/README.md)
// The database is Neon (external), so there is no SQL resource here.
// The AI video worker is NOT deployed to Azure: it runs on a demo laptop and posts its results to the API (see docs/current-progress/TODO.md).
targetScope = 'subscription'

@description('Azure region. The Neon project runs in AWS us-east-2, so eastus2 keeps database round trips short.')
param location string = 'eastus2'

@description('Prefix for every resource name.')
@minLength(3)
@maxLength(12)
param appName string = 'pakar'

// ---- Images (built and pushed by CI, issue #21). Each container must listen on port 8080. ----
param apiImage string = 'ghcr.io/amarelyy/pakar-api:latest'
param webImage string = 'ghcr.io/amarelyy/pakar-web:latest'

@description('Registry that holds the images. Leave the user name empty when the images are public.')
param registryServer string = 'ghcr.io'
param registryUsername string = ''
@secure()
param registryPassword string = ''

// ---- Secrets: written to Key Vault, never into the template or git. ----
@description('Email of the first Admin account (created on first start when it does not exist).')
param adminEmail string
@secure()
@description('JWT signing key, at least 32 bytes. Key Vault name: Jwt--Key')
param jwtKey string
@secure()
@description('Shared key the video worker sends in X-Api-Key, at least 32 characters. Key Vault name: Ingest--ApiKey')
param ingestApiKey string
@secure()
@description('Password of the first Admin account, at least 12 characters. Key Vault name: Admin--Password')
param adminPassword string
@secure()
@description('Neon pooled connection URL used by the API. Key Vault name: DATABASE-URL')
param databaseUrl string
@secure()
@description('Neon direct (unpooled) URL used by the migration bundle. Key Vault name: DATABASE-URL-UNPOOLED')
param databaseUrlUnpooled string

// ---- Options ----
@description('Web can scale to zero to save money (first visit after idle is slow). The API always keeps one replica.')
@minValue(0)
@maxValue(1)
param webMinReplicas int = 0
@description('Keep true: the video worker runs on a laptop outside Azure and reaches the API through this public address.')
param apiExternalIngress bool = true

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: '${appName}-rg'
  location: location
}

module resources 'resources.bicep' = {
  name: 'pakar-resources'
  scope: rg
  params: {
    location: location
    appName: appName
    apiImage: apiImage
    webImage: webImage
    registryServer: registryServer
    registryUsername: registryUsername
    registryPassword: registryPassword
    adminEmail: adminEmail
    jwtKey: jwtKey
    ingestApiKey: ingestApiKey
    adminPassword: adminPassword
    databaseUrl: databaseUrl
    databaseUrlUnpooled: databaseUrlUnpooled
    webMinReplicas: webMinReplicas
    apiExternalIngress: apiExternalIngress
  }
}

output resourceGroupName string = rg.name
output webUrl string = resources.outputs.webUrl
output apiUrl string = resources.outputs.apiUrl
output keyVaultName string = resources.outputs.keyVaultName
