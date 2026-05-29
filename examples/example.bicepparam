using './gen-kv-secret.bicep'

param generatorId = 'test'
param keyVaultName = 'kv-ad-prod-we'
param secretName = 'gen-kv-secret-extension-sample'
param secretProperties = {
  ContentType: 'local username: smurf'
}
