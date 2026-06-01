metadata name = 'Generate Key Vault Secret'
metadata description = 'Generate a random password and store it in an Azure Key Vault secret. This module uses the gen-kv-secret local extension to create or update a secret with the generated password value and specified properties.'

targetScope = 'local'

extension genkvsecret

/// PARAMETERS ///
@description('Unique id for the generated password resource instance.')
@minLength(4)
@maxLength(64)
param generatorId string = 'sample-password'

@description('Target Key Vault name that will receive the generated secret.')
@minLength(3)
param keyVaultName string

@description('Target Key Vault secret name to create or update.')
@minLength(1)
param secretName string

@description('Requested password length.')
@minValue(12)
param length int = 24

@description('Minimum uppercase letters required in the generated password.')
@minValue(0)
param minUpper int = 2

@description('Minimum lowercase letters required in the generated password.')
@minValue(0)
param minLower int = 2

@description('Minimum digits required in the generated password.')
@minValue(0)
param minDigits int = 2

@description('Minimum special characters required in the generated password.')
@minValue(0)
param minSpecial int = 2

@description('Optional characters to exclude from all sets (for example: O0Il).')
param excludeChars string = 'oO0lI,./'

@description('Optional explicit set of allowed special characters.')
param allowedSpecialChars string = ''

@description('Optional Key Vault properties object.')
#disable-next-line secure-secrets-in-params 
param secretProperties secretPropertiesType = {
  Enabled: true
}

@description('Whether to overwrite the existing secret. Set to false to keep the existing secret and return its URI and version without changes.')
param overwrite bool = true

/// VARIABLES ///


/// RESOURCES ///
resource generatedSecret 'Generate-KV-Secret' = {
  id: generatorId
  length: length
  minUpper: minUpper
  minLower: minLower
  minDigits: minDigits
  minSpecial: minSpecial
  keyVaultName: keyVaultName
  secretName: secretName
  excludeChars: excludeChars
  allowedSpecialChars: allowedSpecialChars
  secretProperties: {
    enabled: secretProperties.?Enabled ?? true
    notBefore: secretProperties.?NotBefore
    expiresOn: secretProperties.?ExpiresOn
    contentType: secretProperties.?ContentType
  }
  overwrite: overwrite
}

/// OUTPUTS ///
@description('Secret URI written by the local extension.')
output secretUri string = generatedSecret.secretUri!

@description('Secret version written by the local extension.')
output secretVersion string = generatedSecret.secretVersion!

/// DEFINITIONS ///
@description('Optional Key Vault secret properties aligned with Azure .NET SDK naming.')
type secretPropertiesType = {
  @description('Whether the Key Vault secret is enabled. Defaults to true when omitted.')
  Enabled: bool?

  @description('Not-before timestamp in ISO 8601 format.')
  NotBefore: string?

  @description('Expiration timestamp in ISO 8601 format.')
  ExpiresOn: string?

  @description('Optional secret content type.')
  ContentType: string?
}
