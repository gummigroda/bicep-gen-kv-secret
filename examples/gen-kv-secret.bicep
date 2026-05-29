metadata name = 'gen-kv-secret-consumer-sample'
metadata description = 'Minimal local-deploy example that generates one password and stores it directly in Key Vault.'

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
param excludeChars string = ''

@description('Optional explicit set of allowed special characters.')
param allowedSpecialChars string = ''

@description('Optional Key Vault properties object aligned to Azure .NET SDK naming for secret attributes.')
#disable-next-line secure-secrets-in-params 
param secretProperties secretPropertiesType = {
  Enabled: true
}

/// VARIABLES ///
var hasAllowedSpecialChars = !empty(allowedSpecialChars)
var hasExcludeChars = !empty(excludeChars)
var hasSecretNotBefore = !empty(secretProperties.?NotBefore ?? '')
var hasSecretExpiresOn = !empty(secretProperties.?ExpiresOn ?? '')
var hasSecretContentType = !empty(secretProperties.?ContentType ?? '')

/// RESOURCES ///
resource generatedPassword 'Generate-KV-Secret' = {
  id: generatorId
  length: length
  minUpper: minUpper
  minLower: minLower
  minDigits: minDigits
  minSpecial: minSpecial
  keyVaultName: keyVaultName
  secretName: secretName
  excludeChars: hasExcludeChars ? excludeChars : null
  allowedSpecialChars: hasAllowedSpecialChars ? allowedSpecialChars : null
  secretProperties: {
    enabled: secretProperties.?Enabled ?? true
    notBefore: hasSecretNotBefore ? secretProperties.?NotBefore : null
    expiresOn: hasSecretExpiresOn ? secretProperties.?ExpiresOn : null
    contentType: hasSecretContentType ? secretProperties.?ContentType : null
  }
}

/// OUTPUTS ///
@description('Secret URI written by the local extension.')
output secretUri string = generatedPassword.secretUri!

@description('Secret version written by the local extension.')
output secretVersion string = generatedPassword.secretVersion!

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
