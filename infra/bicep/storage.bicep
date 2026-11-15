// Optional (parameter deployVideoStorage): an Azure Files share for the video files, mounted read-only in the worker.
// Upload videos with: az storage file upload --account-name <name> --share-name videos --source lot-a.mp4
param location string
param appName string
param envName string

var accountName = take('${toLower(replace(appName, '-', ''))}st${uniqueString(resourceGroup().id)}', 24)

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: accountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: account
  name: 'default'
}

resource share 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: 'videos'
  properties: {
    shareQuota: 5
  }
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: envName
}

resource envStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: env
  name: 'videos'
  properties: {
    azureFile: {
      accountName: account.name
      accountKey: account.listKeys().keys[0].value
      shareName: share.name
      accessMode: 'ReadOnly'
    }
  }
}

output storageName string = envStorage.name
output storageAccountName string = account.name
