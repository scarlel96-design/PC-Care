using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SmartPerformanceDoctor.App.Services;

internal static class RuntimeAuthenticodeVerifier
{
    private static readonly string[] TrustedPublisherMarkers =
    [
        "Smart Performance Doctor Dev",
        "PC Care",
        "PCCare"
    ];

    public static (RuntimeTrustLevel Level, string Summary, IReadOnlyList<string> UnsignedFiles) EvaluateInstallRoot(string root)
    {
        var unsigned = new List<string>();
        var untrusted = new List<string>();

        foreach (var relative in EnumerateCriticalBinaries(root))
        {
            var fullPath = Path.Combine(root, relative);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            if (!TryVerifyAuthenticode(fullPath, out var subject))
            {
                unsigned.Add(relative);
                continue;
            }

            if (!IsTrustedPublisher(subject))
            {
                untrusted.Add($"{relative} ({subject})");
            }
        }

        if (unsigned.Count > 0)
        {
            return (RuntimeTrustLevel.Degraded, $"unsigned:{unsigned.Count}", unsigned);
        }

        if (untrusted.Count > 0)
        {
            return (RuntimeTrustLevel.Degraded, $"untrusted-publisher:{untrusted.Count}", untrusted);
        }

        return (RuntimeTrustLevel.Full, "authenticode-verified", Array.Empty<string>());
    }

    public static bool TryVerifyAuthenticode(string path, out string? subject)
    {
        subject = null;
        try
        {
#pragma warning disable SYSLIB0057
            var cert = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            if (cert is null)
            {
                return false;
            }

            using var x509 = new X509Certificate2(cert);
            subject = x509.Subject;
            return !string.IsNullOrWhiteSpace(subject);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsTrustedPublisher(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return false;
        }

        return TrustedPublisherMarkers.Any(marker =>
            subject.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateCriticalBinaries(string root)
    {
        yield return "PCCare.exe";
        yield return "SmartPerformanceDoctor.exe";
        yield return "AstraCare.exe";
        yield return "SmartPerformanceDoctor.dll";
        yield return Path.Combine("engine", "smart_performance_doctor_core.exe");
        yield return Path.Combine("engine", "smart_performance_doctor_repair_helper.exe");
        yield return Path.Combine("engine", "AstraCore.exe");
        yield return Path.Combine("engine", "AstraRepairHelper.exe");
        yield return Path.Combine("engine", "AegisRecoveryHelper.exe");
        yield return Path.Combine("engine", "AegisRecoveryService.exe");
    }
}