using System.Security.AccessControl;
using System.Security.Principal;

namespace SmartPerformanceDoctor.App.Services.Security;

public sealed class SecureVaultAclStatus
{
    public bool Applied { get; init; }
    public string Message { get; init; } = "";
}

internal static class SecureVaultAclHelper
{
    public static SecureVaultAclStatus HardenVaultDirectory()
    {
        try
        {
            var root = SecureVaultPaths.Root;
            if (!Directory.Exists(root))
            {
                return new SecureVaultAclStatus { Applied = false, Message = "금고 디렉터리 없음" };
            }

            var identity = WindowsIdentity.GetCurrent();
            var userSid = identity.User;
            if (userSid is null)
            {
                return new SecureVaultAclStatus { Applied = false, Message = "사용자 SID 확인 불가" };
            }

            ApplyRestrictedAcl(root, userSid);
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                ApplyRestrictedAcl(dir, userSid);
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                ApplyRestrictedFileAcl(file, userSid);
            }

            return new SecureVaultAclStatus
            {
                Applied = true,
                Message = "NTFS ACL: 현재 사용자 전용"
            };
        }
        catch (Exception ex)
        {
            return new SecureVaultAclStatus
            {
                Applied = false,
                Message = $"ACL 적용 실패: {ex.Message}"
            };
        }
    }

    private static void ApplyRestrictedAcl(string directoryPath, SecurityIdentifier userSid)
    {
        var dirInfo = new DirectoryInfo(directoryPath);
        var security = dirInfo.GetAccessControl(AccessControlSections.Access);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.PurgeAccessRules(userSid);

        security.AddAccessRule(new FileSystemAccessRule(
            userSid,
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

        dirInfo.SetAccessControl(security);
    }

    private static void ApplyRestrictedFileAcl(string filePath, SecurityIdentifier userSid)
    {
        var fileInfo = new FileInfo(filePath);
        var security = fileInfo.GetAccessControl(AccessControlSections.Access);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.PurgeAccessRules(userSid);

        security.AddAccessRule(new FileSystemAccessRule(
            userSid,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        fileInfo.SetAccessControl(security);
    }
}