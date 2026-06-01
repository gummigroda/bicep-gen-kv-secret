using System.Security.Cryptography;
using System.Globalization;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Bicep.Local.Extension.Host.Handlers;

namespace GenKvSecretExtension.Handlers;

public class PasswordHandler : TypedResourceHandler<PasswordResource, PasswordResourceIdentifiers>
{
    private static readonly DefaultAzureCredential Credential = new();

    private const int MinimumLength = 12;
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string DefaultSpecial = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    protected override async Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        ValidatePolicy(request.Properties);
        request.Properties.Value = null;
        request.Properties.SecretUri = BuildSecretUri(request.Properties.KeyVaultName, request.Properties.SecretName);
        request.Properties.SecretVersion = null;
        return GetResponse(request);
    }

    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        ValidatePolicy(request.Properties);

        Response<KeyVaultSecret> secretResponse;

        if (request.Properties.Overwrite)
        {
            var generatedPassword = GeneratePassword(request.Properties);
            secretResponse = await SetSecretInKeyVault(request.Properties, generatedPassword, cancellationToken);
        }
        else
        {
            secretResponse = await GetExistingSecret(request.Properties, cancellationToken);
        }

        request.Properties.Value = null;
        request.Properties.SecretUri = secretResponse.Value.Id.AbsoluteUri;
        request.Properties.SecretVersion = secretResponse.Value.Properties.Version;

        return GetResponse(request);
    }

    protected override PasswordResourceIdentifiers GetIdentifiers(PasswordResource properties)
        => new()
        {
            Id = properties.Id
        };

    private static void ValidatePolicy(PasswordResource resource)
    {
        ValidateKeyVaultInputs(resource);

        if (resource.Length < MinimumLength)
        {
            resource.Length = MinimumLength;
        }

        if (resource.MinUpper < 0 || resource.MinLower < 0 || resource.MinDigits < 0 || resource.MinSpecial < 0)
        {
            throw new InvalidOperationException("Minimum class requirements must be zero or positive.");
        }

        var minimumSum = resource.MinUpper + resource.MinLower + resource.MinDigits + resource.MinSpecial;
        if (minimumSum > resource.Length)
        {
            throw new InvalidOperationException("Length must be greater than or equal to sum of minimum class requirements.");
        }

        _ = BuildAvailableCharacterSets(resource);
    }

    private static void ValidateKeyVaultInputs(PasswordResource resource)
    {
        if (string.IsNullOrWhiteSpace(resource.KeyVaultName))
        {
            throw new InvalidOperationException("keyVaultName is required and cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(resource.SecretName))
        {
            throw new InvalidOperationException("secretName is required and cannot be empty.");
        }
    }

    private static string BuildSecretUri(string keyVaultName, string secretName)
        => $"https://{keyVaultName}.vault.azure.net/secrets/{secretName}";

    private static async Task<Response<KeyVaultSecret>> SetSecretInKeyVault(PasswordResource resource, string password, CancellationToken cancellationToken)
    {
        var vaultUri = new Uri($"https://{resource.KeyVaultName}.vault.azure.net/");
        var client = new SecretClient(vaultUri, Credential);
        var secretProperties = resource.SecretProperties;
        var secret = new KeyVaultSecret(resource.SecretName, password)
        {
            Properties =
            {
                Enabled = secretProperties?.Enabled ?? true,
                NotBefore = ParseOptionalDateTimeOffset(secretProperties?.NotBefore, "SecretProperties.NotBefore"),
                ExpiresOn = ParseOptionalDateTimeOffset(secretProperties?.ExpiresOn, "SecretProperties.ExpiresOn"),
                ContentType = string.IsNullOrWhiteSpace(secretProperties?.ContentType) ? null : secretProperties.ContentType
            }
        };

        try
        {
            return await client.SetSecretAsync(secret, cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            throw new InvalidOperationException(
                $"Failed to write secret '{resource.SecretName}' to Key Vault '{resource.KeyVaultName}'. " +
                $"Status: {exception.Status}. ErrorCode: {exception.ErrorCode}. Message: {exception.Message}",
                exception);
        }
    }

    private static async Task<Response<KeyVaultSecret>> GetExistingSecret(PasswordResource resource, CancellationToken cancellationToken)
    {
        var vaultUri = new Uri($"https://{resource.KeyVaultName}.vault.azure.net/");
        var client = new SecretClient(vaultUri, Credential);

        try
        {
            return await client.GetSecretAsync(resource.SecretName, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve secret '{resource.SecretName}' from Key Vault '{resource.KeyVaultName}'. " +
                $"Status: {exception.Status}. ErrorCode: {exception.ErrorCode}. Message: {exception.Message}",
                exception);
        }
    }

    private static DateTimeOffset? ParseOptionalDateTimeOffset(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new InvalidOperationException($"{propertyName} must be a valid ISO 8601 datetime string.");
        }

        return parsed;
    }

    private static string GeneratePassword(PasswordResource resource)
    {
        var sets = BuildAvailableCharacterSets(resource);
        var special = sets.Special;
        var availableAll = sets.All;

        var chars = new List<char>(resource.Length);

        AddChars(chars, sets.Upper, resource.MinUpper);
        AddChars(chars, sets.Lower, resource.MinLower);
        AddChars(chars, sets.Digits, resource.MinDigits);
        AddChars(chars, special, resource.MinSpecial);

        while (chars.Count < resource.Length)
        {
            chars.Add(PickRandom(availableAll));
        }

        Shuffle(chars);
        return new string(chars.ToArray());
    }

    private static (char[] Upper, char[] Lower, char[] Digits, char[] Special, char[] All) BuildAvailableCharacterSets(PasswordResource resource)
    {
        var excluded = resource.ExcludeChars ?? string.Empty;
        var specialsSource = string.IsNullOrEmpty(resource.AllowedSpecialChars) ? DefaultSpecial : resource.AllowedSpecialChars;

        var upper = FilterSet(Upper, excluded);
        var lower = FilterSet(Lower, excluded);
        var digits = FilterSet(Digits, excluded);
        var special = FilterSet(specialsSource!, excluded);

        if (resource.MinUpper > 0 && upper.Length == 0)
        {
            throw new InvalidOperationException("No uppercase characters available after exclusions.");
        }

        if (resource.MinLower > 0 && lower.Length == 0)
        {
            throw new InvalidOperationException("No lowercase characters available after exclusions.");
        }

        if (resource.MinDigits > 0 && digits.Length == 0)
        {
            throw new InvalidOperationException("No digit characters available after exclusions.");
        }

        if (resource.MinSpecial > 0 && special.Length == 0)
        {
            throw new InvalidOperationException("No special characters available after exclusions.");
        }

        var all = upper
            .Concat(lower)
            .Concat(digits)
            .Concat(special)
            .Distinct()
            .ToArray();

        if (all.Length == 0)
        {
            throw new InvalidOperationException("No characters available to generate a password.");
        }

        return (upper, lower, digits, special, all);
    }

    private static char[] FilterSet(string source, string excluded)
        => source.Where(ch => !excluded.Contains(ch)).Distinct().ToArray();

    private static void AddChars(List<char> target, char[] source, int count)
    {
        for (var i = 0; i < count; i++)
        {
            target.Add(PickRandom(source));
        }
    }

    private static char PickRandom(char[] source)
    {
        var index = RandomNumberGenerator.GetInt32(0, source.Length);
        return source[index];
    }

    private static void Shuffle(List<char> chars)
    {
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(0, i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
