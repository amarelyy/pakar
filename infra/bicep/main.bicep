targetScope = 'subscription'

param location string = 'southeastasia'
param appName string = 'pakar'

@secure()
param sqlAdminPassword string

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${appName}-rg'
  location: location
}

module resources 'resources.bicep' = {
  name: 'linkedDeployment'
  scope: rg 
  params: {
    location: location
    appName: appName
    sqlAdminPassword: sqlAdminPassword
  }
}
