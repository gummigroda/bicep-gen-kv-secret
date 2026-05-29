//Scope:resourceGroup:rg-acr:01609447-26b5-46f7-bd9f-c499c52ce646
using 'acr.bicep'

param acrConfig = {
  name: readEnvironmentVariable('AZURE_REGISTRY_NAME', 'CHANGE-ME')
  location: 'Sweden Central'
  sku: 'Standard'
  anonymousPullEnabled: true
}
