using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace SmartPerformanceDoctor.App.Services;

internal static class RuntimeAuthenticodeVerifier
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    private const int TrustENoSignature = unchecked((int)0x800B0100);

    // Release publisher pins must be provisioned through a reviewed release process.
    // An arbitrary valid Authenticode signer is not the PC-Care publisher.
    private static readonly HashSet<string> TrustedPublisherThumbprints = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref WinTrustData data);

    public static (RuntimeTrustLevel Level, string Summary, IReadOnlyList<string> UnsignedFiles)
        EvaluateInstallRoot(string root)
    {
        var unverified = new List<string>();
        var failed = new List<string>();
        var checkedCount = 0;
        foreach (var relative in EnumerateCriticalBinaries())
        {
            var path = Path.Combine(root, relative);
            if (!File.Exists(path))
            {
                if (relative is "PCCare.exe" or "SmartPerformanceDoctor.dll")
                    failed.Add(relative + ":missing");
                continue;
            }
            checkedCount++;
            var result = Verify(path, out var thumbprint);
            if (result == TrustENoSignature)
            {
                unverified.Add(relative + ":unsigned");
            }
            else if (result != 0)
            {
                failed.Add(relative + ":signature-failed");
            }
            else if (thumbprint is null || !TrustedPublisherThumbprints.Contains(thumbprint))
            {
                unverified.Add(relative + ":publisher-unpinned");
            }
        }

        if (failed.Count > 0)
            return (RuntimeTrustLevel.Failed, $"signature-failed:{failed.Count}", failed);
        if (unverified.Count > 0 || checkedCount == 0)
            return (RuntimeTrustLevel.Unverified, $"publisher-unverified:{unverified.Count}", unverified);
        return (RuntimeTrustLevel.Verified, "authenticode-and-publisher-verified", Array.Empty<string>());
    }

    public static bool TryVerifyAuthenticode(string path, out string? subject)
    {
        var code = Verify(path, out _);
        subject = code == 0 ? "Windows Authenticode policy passed" : null;
        return code == 0;
    }

    private static int Verify(string path, out string? thumbprint)
    {
        thumbprint = null;
        if (!OperatingSystem.IsWindows() || !File.Exists(path))
            return -1;

        var fileInfo = new WinTrustFileInfo
        {
            Size = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
            FilePath = path,
            FileHandle = IntPtr.Zero,
            KnownSubject = IntPtr.Zero
        };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(fileInfo, pointer, fDeleteOld: false);
            var data = new WinTrustData
            {
                Size = (uint)Marshal.SizeOf<WinTrustData>(),
                UiChoice = 2, // WTD_UI_NONE
                RevocationChecks = 1, // WTD_REVOKE_WHOLECHAIN
                UnionChoice = 1, // WTD_CHOICE_FILE
                FileInfo = pointer,
                ProviderFlags = 0x40 // WTD_REVOCATION_CHECK_CHAIN
            };
            var action = GenericVerifyV2;
            var result = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            if (result == 0)
            {
                try
                {
#pragma warning disable SYSLIB0057
                    using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                    thumbprint = certificate.Thumbprint;
                }
                catch
                {
                    // A catalog-signed file can pass policy without an embedded signer.
                }
            }
            return result;
        }
        catch
        {
            return -1;
        }
        finally
        {
            Marshal.DestroyStructure<WinTrustFileInfo>(pointer);
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static IEnumerable<string> EnumerateCriticalBinaries()
    {
        yield return "PCCare.exe";
        yield return "SmartPerformanceDoctor.dll";
        yield return Path.Combine("engine", "smart_performance_doctor_core.exe");
        yield return Path.Combine("engine", "smart_performance_doctor_repair_helper.exe");
        yield return Path.Combine("engine", "AegisRecoveryHelper.exe");
        yield return Path.Combine("engine", "AegisRecoveryService.exe");
    }
}
