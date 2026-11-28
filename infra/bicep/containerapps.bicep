// The two Container Apps: Api (one replica, in-memory realtime state) and Web (Blazor Server).
// Not used. The deployment is the virtual machine in infra/vm.
param location string
param appName string
param environmentId string
param environmentDefaultDomain string
param identityId string
@description('Key Vault address, ends with a slash.')
param vaultUri string

param apiImage string
param webImage string
param registryServer string
param registryUsername string
@secure()
param registryPassword string

param adminEmail string
param webMinReplicas int
param apiExternalIngress bool

var apiName = '${appName}-api'
var webName = '${appName}-web'

// Addresses follow the pattern <app>.<environment domain>, so the apps can know each other's URL before they exist.
var webUrl = 'https://${webName}.${environmentDefaultDomain}'
var apiUrl = apiExternalIngress ? 'https://${apiName}.${environmentDefaultDomain}' : 'https://${apiName}.internal.${environmentDefaultDomain}'

var identityConfig = {
  type: 'UserAssigned'
  userAssignedIdentities: {
    '${identityId}': {}
  }
}

var registries = empty(registryUsername) ? [] : [
  {
    server: registryServer
    username: registryUsername
    passwordSecretRef: 'registry-password'
  }
]
var registrySecrets = empty(registryUsername) ? [] : [
  {
    name: 'registry-password'
    value: registryPassword
  }
]

// Container Apps secret name -> Key Vault secret name.
var apiVaultSecrets = [
  { name: 'jwt-key', vaultName: 'Jwt--Key' }
  { name: 'ingest-api-key', vaultName: 'Ingest--ApiKey' }
  { name: 'admin-password', vaultName: 'Admin--Password' }
  { name: 'database-url', vaultName: 'DATABASE-URL' }
]

var apiSecrets = [for s in apiVaultSecrets: {
  name: s.name
  keyVaultUrl: '${vaultUri}secrets/${s.vaultName}'
  identity: identityId
}]

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiName
  location: location
  identity: identityConfig
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: apiExternalIngress
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: registries
      secrets: concat(apiSecrets, registrySecrets)
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
            // Schema changes are applied by the migration bundle during deployment, not by the app.
            { name: 'Database__ApplyMigrationsOnStartup', value: 'false' }
            { name: 'Cors__Origins__0', value: webUrl }
            { name: 'Admin__Email', value: adminEmail }
            { name: 'DATABASE_URL', secretRef: 'database-url' }
            { name: 'Jwt__Key', secretRef: 'jwt-key' }
            { name: 'Ingest__ApiKey', secretRef: 'ingest-api-key' }
            { name: 'Admin__Password', secretRef: 'admin-password' }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 5
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
              periodSeconds: 15
              failureThreshold: 3
            }
          ]
        }
      ]
      // The realtime state (ParkingStateStore) lives in memory, so there must be exactly one replica (team decision 7).
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: webName
  location: location
  identity: identityConfig
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        // Blazor Server keeps a live connection per browser, so a visitor must stay on the same replica.
        stickySessions: {
          affinity: 'sticky'
        }
      }
      registries: registries
      secrets: registrySecrets
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
            { name: 'Api__BaseUrl', value: '${apiUrl}/' }
          ]
          probes: [
            {
              type: 'Startup'
              tcpSocket: { port: 8080 }
              periodSeconds: 5
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              tcpSocket: { port: 8080 }
              periodSeconds: 30
              failureThreshold: 3
            }
          ]
        }
      ]
      // One replica at most: ASP.NET data-protection keys are per instance, so more than one would break sign-in cookies.
      scale: {
        minReplicas: webMinReplicas
        maxReplicas: 1
      }
    }
  }
}

output webUrl string = webUrl
output apiUrl string = apiUrl
