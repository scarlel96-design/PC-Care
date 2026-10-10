using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record AuthenticodeResult(int Code, string? Thumbprint, string Identity, string Hash);

/// <summary>Read the signer from the retained WinVerifyTrust provider state.</summary>
public static class AuthenticodeBoundary
{
    public static AuthenticodeResult Verify(string path, Action? whilePinned = null)
    {
        try
        {
            var full = Path.GetFullPath(path);
            using var file = WindowsFileBoundary.OpenRead(Path.GetDirectoryName(full)!, Path.GetFileName(full));
            var hash = file.Hash();
            whilePinned?.Invoke();
            var result = VerifyOpened(full, file);
            file.Validate();
            if (file.Hash() != hash) return new(-1, null, file.Identity, hash);
            return new(result.Code, result.Thumbprint, file.Identity, hash);
        }
        catch { return new(-1, null, "", ""); }
    }

    public static bool IsBuildPublisher(AuthenticodeResult result) => result.Code == 0 &&
        result.Thumbprint is not null && BuildPublisherTrust.ExecutableThumbprints.Contains(result.Thumbprint);

    private static (int Code, string? Thumbprint) VerifyOpened(string path, WindowsFileBoundary.FileLease file)
    {
        var info = new FileInfoData { Size = (uint)Marshal.SizeOf<FileInfoData>(), Path = path,
            Handle = file.Stream.SafeFileHandle.DangerousGetHandle() };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoData>());
        var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Revocation = 1,
            Choice = 1, File = pointer, StateAction = 1, Flags = 0x40 };
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try
        {
            Marshal.StructureToPtr(info, pointer, false);
            file.Validate();
            var code = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            file.Validate();
            if (code != 0) return (code, null);
            var provider = WTHelperProvDataFromStateData(data.State);
            var signer = provider == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            if (signer == IntPtr.Zero) return (-1, null);
            var signerData = Marshal.PtrToStructure<SignerData>(signer);
            if (signerData.Error != 0 || signerData.Count == 0) return (-1, null);
            var cert = WTHelperGetProvCertFromChain(signer, 0);
            if (cert == IntPtr.Zero) return (-1, null);
            // CRYPT_PROVIDER_CERT starts with cbStruct and aligned PCCERT_CONTEXT.
            var context = Marshal.ReadIntPtr(cert, IntPtr.Size == 8 ? 8 : 4);
            if (context == IntPtr.Zero) return (-1, null);
#pragma warning disable SYSLIB0057
            using var certificate = new X509Certificate2(context);
#pragma warning restore SYSLIB0057
            file.Validate();
            return (0, certificate.Thumbprint);
        }
        finally
        {
            data.StateAction = 2;
            WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            Marshal.DestroyStructure<FileInfoData>(pointer);
            Marshal.FreeHGlobal(pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfoData { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path;
        public IntPtr Handle, Subject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData { public uint Size; public IntPtr Callback, Sip; public uint Ui, Revocation, Choice;
        public IntPtr File; public uint StateAction; public IntPtr State, Url; public uint Flags, Context;
        public IntPtr Settings; }
    [StructLayout(LayoutKind.Sequential)]
    private struct SignerData { public uint Size; public System.Runtime.InteropServices.ComTypes.FILETIME Time;
        public uint Count; public IntPtr Chain; public uint Type; public IntPtr Signer; public uint Error, CounterCount;
        public IntPtr Counters, ChainContext; }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider, uint signer, [MarshalAs(UnmanagedType.Bool)] bool counter, uint index);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint index);
}
