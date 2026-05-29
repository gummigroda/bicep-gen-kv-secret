# Gen KV Secret Bicep Extension

This repository contains a custom local Bicep extension that generates strong passwords during `bicep local-deploy` and writes them directly to Azure Key Vault.

## What this repo contains

- Local resource type: `Generate-KV-Secret`
- Extension source: `src/`
- Azure DevOps pipeline: `_pipelines/build-and-publish.yml`
- Publish script: `scripts/Publish-GenKvSecretExtension.ps1`
- Example consumer template: `examples/consumer-password-template.bicep`

## Password resource contract

Required properties:

- `id`
- `length`
- `minUpper`
- `minLower`
- `minDigits`
- `minSpecial`

Optional properties:

- `excludeChars`
- `allowedSpecialChars`
- `secretProperties` object (UDT in the sample template)
- `secretProperties.Enabled` (defaults to `true` when omitted)
- `secretProperties.NotBefore` (optional not-before timestamp in ISO 8601)
- `secretProperties.ExpiresOn` (optional expiration timestamp in ISO 8601)
- `secretProperties.ContentType` (optional secret content type)

Example:

```bicep
param secretProperties secretPropertiesType = {
  Enabled: true
  NotBefore: '2026-06-01T00:00:00Z'
  ExpiresOn: '2027-06-01T00:00:00Z'
  ContentType: 'password'
}
```

Required Key Vault properties:

- `keyVaultName`
- `secretName`

Output property:

- `value` (always `null`)
- `secretUri`
- `secretVersion`

Behavior notes:

- Length is clamped to a minimum of `12`.
- The sum of minimum character class requirements must not exceed `length`.
- If exclusions remove all characters from a required class, generation fails.
- The extension writes to `https://{keyVaultName}.vault.azure.net/secrets/{secretName}`.
- If the Key Vault write fails, local deploy fails.
- Plaintext password is never returned in outputs.

## Prerequisites

- .NET SDK `10.0.107` (see `global.json`)
- Azure CLI with Bicep CLI (`az bicep`)
- Bicep local deploy experimental feature enabled in `bicepconfig.json`
- Access to publish/read extension artifacts in ACR

## Build locally

```pwsh
Push-Location src
dotnet restore
dotnet build ./GenKvSecretExtension.csproj --configuration Release
Pop-Location
```

## Run the example template

1. Ensure your working folder uses this repo's `bicepconfig.json` (configured with alias `genkvsecret`).
1. Sign in to Azure with an identity that can set secrets in the target Key Vault.
1. Set Key Vault parameters in `examples/consumer-password-template.bicepparam`.
1. Run local deploy against the example params:

```pwsh
bicep local-deploy ./examples/consumer-password-template.bicepparam --format json
```

Verify secret write:

```pwsh
$result = bicep local-deploy ./examples/consumer-password-template.bicepparam --format json | ConvertFrom-Json
$result.outputs.secretUri.value
$result.outputs.secretVersion.value

az keyvault secret show --vault-name '<your-kv-name>' --name '<your-secret-name>' --query id -o tsv
```

## CI/CD publish flow

Pipeline: `_pipelines/build-and-publish.yml`

GitHub Actions workflow: `.github/workflows/build-and-publish-extension.yml`

- Build stage publishes binaries for:
  - `linux-x64`
  - `linux-arm64`
  - `win-x64`
  - `osx-arm64`
- Publish stage packages and pushes extension with:
  - `bicep publish-extension`

GitHub Actions notes:

- Triggered on push to `main`, `feature/**`, and `fix/**` branches for extension source changes, and manually via `workflow_dispatch`.
- Uses OIDC auth with `azure/login@v2`.
- Runs `dotnet/nbgv@v0.4.2` to compute branch-aware version metadata.
- Uses `scripts/Publish-GenKvSecretExtension.ps1` to package and publish.

Required GitHub secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Optional GitHub repository variables (defaults shown):

- `ACR_NAME` (default: `azlost`)
- `EXTENSION_NAME` (default: `gen-kv-secret`)

Versioning:

- Base version comes exclusively from Nerdbank.GitVersioning (`dotnet/nbgv` output `SimpleVersion`).
- `main` branch publishes stable: `x.y.z`.
- `feature/**` and `fix/**` branches publish preview: `x.y.z-preview`.

Note: if your configured extension reference does not exist in ACR yet, publish the extension first or point `bicepconfig.json` to a valid local/registry extension target.

## Publishing a new version

1. Commit and push to `main` (stable), `feature/**`, or `fix/**` (preview).
2. The workflow automatically runs `dotnet/nbgv` to compute the version from git history and `version.json`.
3. Pipeline publishes extension to ACR with the resolved version.

## Authentication and authorization

Authentication is handled by `DefaultAzureCredential` inside the extension handler. During local runs, this typically resolves credentials from your existing developer sign-in (for example `az login`, VS Code Azure account, or environment-based service principal). In hosted Azure environments, managed identity and environment credentials are used.

Authorization is Key Vault data-plane RBAC. The executing identity needs permission to set secrets on the target vault. The minimum built-in role for this flow is `Key Vault Secrets Officer` at Key Vault scope.

Quick checks:

```pwsh
az account show
az role assignment list --scope <key-vault-resource-id> --assignee <principal-object-id> --output table
```

## Security notes

- Do not print generated passwords to logs from consumer scripts.
- The extension never returns plaintext password in output.
- Ensure Key Vault networking allows the machine or environment running local deploy.
- On macOS, unsigned binaries can require local `codesign` before first execution.

## Using This Repository

### Published Extension (Recommended for Users)

The extension is published to a public ACR (`azlost.azurecr.io`) for easy consumption:

```bicep
// bicepconfig.json
{
  "extensions": {
    "genkvsecret": "br:azlost.azurecr.io/extensions/gen-kv-secret:0.2.0"
  }
}
```

### Forking for Custom ACR

If you want to fork this repository and publish to your own ACR:

1. **Fork the repo** on GitHub
2. **Set up your ACR** (see [docs/FORKING.md](docs/FORKING.md))
3. **Configure GitHub secrets/variables**:
   - `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
   - `ACR_NAME` (your registry name), `EXTENSION_NAME` (optional)
4. **Push to branches** (`main`, `feature/**`, `fix/**`) — GitHub Actions automatically publishes to your ACR

**Example**: After forking and setting up, push a change:

```bash
git clone https://github.com/<your-org>/bicep-gen-kv-secret.git
cd bicep-gen-kv-secret
git checkout -b feature/my-changes
# Make changes...
git push origin feature/my-changes
# Automatically publishes to your ACR as: your-registry.azurecr.io/extensions/gen-kv-secret:0.2.0-preview
```

For detailed instructions, see [docs/FORKING.md](docs/FORKING.md).

### Build Locally

To compile and test without publishing to ACR:

```pwsh
Push-Location src
dotnet restore
dotnet build ./GenKvSecretExtension.csproj --configuration Release

# Publish binaries for all platforms
foreach ($rid in @('linux-x64', 'linux-arm64', 'win-x64', 'osx-arm64')) {
  dotnet publish ./GenKvSecretExtension.csproj `
    --configuration Release `
    --runtime $rid `
    --self-contained `
    -o ../artifacts/$rid
}
Pop-Location
```

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines, and [SECURITY.md](SECURITY.md) for security disclosures.

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
