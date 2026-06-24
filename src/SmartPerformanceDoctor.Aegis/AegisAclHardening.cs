using System.Security.AccessControl;
using System.Security.Principal;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisAclHardening
{
    public static void ApplyMirrorRootAcls()
    {
        if (!OperatingSystem.IsWindows() || AegisMirrorPaths.UsesTestOverride)
        {
            return;
        }

        AegisMirrorPaths.EnsureLayout();
        ApplyDirectoryAcl(AegisMirrorPaths.Root);
        foreach (var dir in new[]
        {
            AegisMirrorPaths.LastKnownGoodDirectory,
            AegisMirrorPaths.StagingDirectory,
            AegisMirrorPaths.QuarantineDirectory,
            AegisMirrorPaths.LogsDirectory,
            AegisMirrorPaths.OfflineDirectory,
            AegisMirrorPaths.ActiveSlotDirectory,
            AegisMirrorPaths.BackupSlotDirectory
        })
        {
            if (Directory.Exists(dir))
            {
                ApplyDirectoryAcl(dir);
            }
        }
    }

    private static void ApplyDirectoryAcl(string path)
    {
        try
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            var info = new DirectoryInfo(path) { Attributes = FileAttributes.Directory };
            info.SetAccessControl(security);
        }
        catch
        {
            // ACL 강화는 best-effort — 설치/복구 흐름을 막지 않습니다.
        }
    }
}