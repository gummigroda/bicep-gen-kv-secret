# Forking and Publishing to Your Own ACR

This guide explains how to fork the `bicep-gen-kv-secret` repository and publish the extension to your own Azure Container Registry (ACR).

## Why Fork?

You might fork this repo if you want to:
- Customize the extension for your organization
- Publish to a private ACR for internal use
- Maintain your own version with custom features
- Contribute improvements back to the main project

## Step 1: Fork the Repository

1. Navigate to [bicep-gen-kv-secret on GitHub](https://github.com/<your-org>/bicep-gen-kv-secret)
2. Click **Fork** in the top-right corner
3. Choose your GitHub organization/account as the destination

## Step 2: Prepare Your Azure Environment

### Create or Use an Existing ACR

```bash
# Create a new ACR (if needed)
az acr create \
  --resource-group <your-rg> \
  --name <your-registry-name> \
  --sku Standard \
  --location <region>

# Example: az acr create --resource-group my-rg --name myregistry --sku Standard
```

### Create Service Principal or Use Managed Identity

**Option A: Service Principal (for GitHub Actions)**

```bash
# Create service principal
az ad sp create-for-rbac \
  --name "gh-bicep-gen-kv-secret-publisher" \
  --role "AcrPush" \
  --scopes "/subscriptions/<subscription-id>/resourceGroups/<rg>/providers/Microsoft.ContainerRegistry/registries/<registry-name>"
```

Save the output (client ID, password, tenant ID).

**Option B: Workload Identity Federation (Recommended)**

Use GitHub OIDC token exchange (no passwords stored):

```bash
# Setup script in this repo: github-workflows/scripts/setup-oidc-federation.ps1
./github-workflows/scripts/setup-oidc-federation.ps1 `
  -AppName "gh-bicep-gen-kv-secret" `
  -GitHubOrg "<your-github-org>" `
  -GitHubRepo "bicep-gen-kv-secret" `
  -SubscriptionId "<subscription-id>" `
  -ResourceGroupName "<rg-name>" `
  -AcrName "<registry-name>"
```

## Step 3: Configure GitHub Secrets & Variables

### Repository Variables (Settings → Variables)

```bash
ACR_NAME = <your-registry-name>
EXTENSION_NAME = gen-kv-secret  # or customize
```

### Repository Secrets (Settings → Secrets)

**If using OIDC (Recommended)**:
```
AZURE_CLIENT_ID = <app-id-from-step-2>
AZURE_TENANT_ID = <tenant-id>
AZURE_SUBSCRIPTION_ID = <subscription-id>
```

**If using Service Principal (Legacy)**:
```
AZURE_CREDENTIALS = {
  "clientId": "...",
  "clientSecret": "...",
  "subscriptionId": "...",
  "tenantId": "..."
}
```

## Step 4: Update Configuration Files (Optional)

### bicepconfig.json

Update with your registry:

```json
{
  "experimentalFeaturesEnabled": {
    "localDeploy": true
  },
  "moduleAliases": {
    "br": {
      "genkvsecret": {
        "registry": "<your-registry-name>.azurecr.io"
      }
    }
  },
  "extensions": {
    "genkvsecret": "br:genkvsecret/extensions/gen-kv-secret:0.2.0"
  }
}
```

### examples/gen-kv-secret.bicepparam

Update with your Key Vault details:

```bicep
param keyVaultName = 'your-kv-name'
param secretName = 'generated-password'
```

## Step 5: Push a Change and Publish

The GitHub Actions workflow automatically publishes on push:

```bash
# Clone your fork
git clone https://github.com/<your-org>/bicep-gen-kv-secret.git
cd bicep-gen-kv-secret

# Create a feature branch
git checkout -b feature/my-changes

# Make your changes (optional)
# Example: Update src/Handlers/PasswordHandler.cs

# Commit and push
git add .
git commit -m "feat: customize for our org"
git push origin feature/my-changes
```

**Expected behavior**:
- GitHub Actions workflow runs automatically
- Builds binaries for all platforms
- Publishes to your ACR as `my-registry.azurecr.io/extensions/gen-kv-secret:0.2.0-preview`
- Check workflow logs in Actions tab for details

## Step 6: Test Your Published Extension

### Verify in ACR

```bash
# List repositories
az acr repository list --name <your-registry-name>

# Show tags
az acr repository show-tags \
  --name <your-registry-name> \
  --repository extensions/gen-kv-secret
```

### Test with bicep local-deploy

```bash
# Ensure you're authenticated to ACR
az acr login --name <your-registry-name>

# Run example
bicep local-deploy ./examples/gen-kv-secret.bicepparam --format json
```

## Customization Options

### Change Extension Name

Update these files:
1. **GitHub Actions workflow** (`.github/workflows/build-and-publish-extension.yml`):
   - Change `vars.EXTENSION_NAME` default

2. **ADO Pipeline** (`_pipelines/templates/_common.yml`):
   - Change `extensionName: gen-kv-secret`

3. **bicepconfig.json**:
   - Update `"genkvsecret"` alias and `"genkvsecret"` key

### Add Custom Features

Modify source code in `src/`:
- **src/Program.cs**: Extension registration
- **src/Handlers/PasswordHandler.cs**: Password generation logic
- **src/Models.cs**: Resource contract (properties/outputs)

Rebuild and test:
```bash
dotnet build src/GenKvSecretExtension.csproj -c Release
bicep local-deploy ./examples/gen-kv-secret.bicepparam --format json
```

### Update Version

```bash
# Edit version.json
{
  "version": "1.0"
}

# Commit to main for stable release
git checkout main
git add version.json
git commit -m "chore: bump version to 1.0"
git push origin main

# GitHub Actions will publish as 1.0.0 (stable)
```

## Contributing Back

If you've made improvements you'd like to share:

1. **Ensure tests pass** (if applicable)
2. **Update documentation** (README, CONTRIBUTING, etc.)
3. **Open a Pull Request** against the main repository
4. **Reference the issue** if reporting a bug

See [CONTRIBUTING.md](../CONTRIBUTING.md) for detailed guidelines.

## Troubleshooting

### "Extension not found" in Bicep

```bash
# 1. Verify ACR login
az acr login --name <your-registry-name>

# 2. Check extension exists
az acr repository show-tags \
  --name <your-registry-name> \
  --repository extensions/gen-kv-secret

# 3. Verify bicepconfig.json
cat bicepconfig.json  # Check registry URL and version

# 4. Run Bicep with verbose output
bicep local-deploy --format json ... 2>&1 | ConvertFrom-Json
```

### "Access denied" publishing to ACR

```bash
# Verify role assignment
az role assignment list \
  --assignee <your-principal-id> \
  --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.ContainerRegistry/registries/<acr-name>

# Expected role: AcrPush or Contributor
```

### GitHub Actions workflow fails

1. **Check workflow logs** in Actions tab
2. **Verify secrets/variables** are set correctly in Settings
3. **Test locally**:
   ```bash
   dotnet restore src/GenKvSecretExtension.csproj
   dotnet build src/GenKvSecretExtension.csproj -c Release
   ```

## Next Steps

- 📖 Read [README.md](../README.md) for usage examples
- 🔧 See [CONTRIBUTING.md](../CONTRIBUTING.md) for development guidelines
- 🔒 Review [SECURITY.md](../SECURITY.md) for security best practices
- 📝 Check [CHANGELOG.md](../CHANGELOG.md) for version history

---

**Need help?** Open an issue or discussion in your fork, or reference the main project repository.
