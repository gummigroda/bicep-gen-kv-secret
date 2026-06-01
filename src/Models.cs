using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace GenKvSecretExtension;

public class SecretPropertiesInput
{
    [TypeProperty("Whether the Key Vault secret is enabled. Defaults to true when omitted.", ObjectTypePropertyFlags.None)]
    public bool Enabled { get; set; } = true;

    [TypeProperty("Not-before time in ISO 8601 format (for example: 2026-06-01T00:00:00Z).", ObjectTypePropertyFlags.None)]
    public string? NotBefore { get; set; }

    [TypeProperty("Expiration time in ISO 8601 format (for example: 2027-06-01T00:00:00Z).", ObjectTypePropertyFlags.None)]
    public string? ExpiresOn { get; set; }

    [TypeProperty("Optional secret content type.", ObjectTypePropertyFlags.None)]
    public string? ContentType { get; set; }
}

public class PasswordResourceIdentifiers
{
    [TypeProperty("Unique identifier for this generated password resource.", ObjectTypePropertyFlags.Required | ObjectTypePropertyFlags.Identifier)]
    public required string Id { get; set; }
}

[ResourceType("Generate-KV-Secret")]
public class PasswordResource : PasswordResourceIdentifiers
{
    [TypeProperty("Total password length.", ObjectTypePropertyFlags.Required)]
    public required int Length { get; set; }

    [TypeProperty("Minimum uppercase characters.", ObjectTypePropertyFlags.Required)]
    public required int MinUpper { get; set; }

    [TypeProperty("Minimum lowercase characters.", ObjectTypePropertyFlags.Required)]
    public required int MinLower { get; set; }

    [TypeProperty("Minimum digit characters.", ObjectTypePropertyFlags.Required)]
    public required int MinDigits { get; set; }

    [TypeProperty("Minimum special characters.", ObjectTypePropertyFlags.Required)]
    public required int MinSpecial { get; set; }

    [TypeProperty("Characters to exclude from any class.", ObjectTypePropertyFlags.None)]
    public string? ExcludeChars { get; set; }

    [TypeProperty("Allowed special characters. If omitted, a default secure set is used.", ObjectTypePropertyFlags.None)]
    public string? AllowedSpecialChars { get; set; }

    [TypeProperty("Target Key Vault name where the generated password will be stored as a secret.", ObjectTypePropertyFlags.Required)]
    public required string KeyVaultName { get; set; }

    [TypeProperty("Target Key Vault secret name to create or update.", ObjectTypePropertyFlags.Required)]
    public required string SecretName { get; set; }

    [TypeProperty("Optional secret properties aligned with Azure .NET SDK naming.", ObjectTypePropertyFlags.None)]
    public SecretPropertiesInput? SecretProperties { get; set; }

    [TypeProperty("Whether to overwrite an existing secret in Key Vault. If false, returns the existing secret version and URI. Defaults to true.", ObjectTypePropertyFlags.None)]
    public bool Overwrite { get; set; } = true;

    [TypeProperty("Generated password value. Always null in Key Vault write mode.", ObjectTypePropertyFlags.ReadOnly)]
    public string? Value { get; set; }

    [TypeProperty("URI of the secret written to Key Vault.", ObjectTypePropertyFlags.ReadOnly)]
    public string? SecretUri { get; set; }

    [TypeProperty("Version of the secret written to Key Vault.", ObjectTypePropertyFlags.ReadOnly)]
    public string? SecretVersion { get; set; }
}
