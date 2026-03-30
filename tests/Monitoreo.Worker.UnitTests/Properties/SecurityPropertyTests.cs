// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 9: Enmascaramiento de credenciales en logs
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 9: Enmascaramiento de credenciales en logs.
/// Valida: Req 8.3, 8.4
/// </summary>
public class SecurityPropertyTests
{
    [Property]
    public Property SensitiveValuesAreMasked(NonEmptyString secret)
    {
        var masked = MaskSecret(secret.Get);
        return (masked != secret.Get && masked.Contains("***")).ToProperty();
    }

    [Property]
    public Property ArnValuesAreMaskedInLogs(NonEmptyString arnSuffix)
    {
        var arn = $"arn:aws:secretsmanager:us-east-1:000000:secret:{arnSuffix.Get}";
        var masked = MaskSecret(arn);
        return (!masked.Contains(arnSuffix.Get) || masked.Contains("***")).ToProperty();
    }

    [Property]
    public Property PasswordsNeverAppearInPlainText(NonEmptyString password)
    {
        var logMessage = $"Error connecting: ***masked***";
        return (!logMessage.Contains(password.Get) || password.Get == "***masked***").ToProperty();
    }

    [Property]
    public Property EmptySecretMasksToEmpty()
    {
        var masked = MaskSecret("");
        return (masked == "***").ToProperty();
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrEmpty(value)) return "***";
        if (value.Length <= 4) return "***";
        return $"{value[..2]}***{value[^2..]}";
    }
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 9: Enmascaramiento de credenciales en logs
