using System.Security.AccessControl;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed partial class UpdateTransaction
{
    private void CheckInstallationAcl(System.Security.AccessControl.FileSystemSecurity security)
    {
        if (!_strictAcl) return;
        try { ValidateProtectedSecurity(security,false); }
        catch (UnauthorizedAccessException ex) { throw new InvalidDataException("INSTALLATION-ACL-MISMATCH",ex); }
        catch (InvalidDataException ex) { throw new InvalidDataException("INSTALLATION-ACL-MISMATCH",ex); }
    }
    private void VerifyInstallationFile(WindowsFileBoundary.FileLease file,long length,string hash,string identity)
    {
        try { file.Validate(identity); }
        catch (IOException ex) { throw new InvalidDataException("INSTALLATION-IDENTITY-MISMATCH",ex); }
        if(_strictAcl)CheckInstallationAcl(file.ReadSecurity());
        file.Verify(length,hash,identity);
    }
    private static string InstallationFailureCode(Exception ex) => ex.Message switch
    {
        "INSTALLATION-IDENTITY-MISMATCH" or "POINTER-BINDING" or "POINTER-JOURNAL" => "INSTALLATION-IDENTITY-MISMATCH",
        "INSTALLATION-ACL-MISMATCH" => "INSTALLATION-ACL-MISMATCH",
        "INSTALLATION-SIGNATURE-REJECTED" or "DECLARATION-UNTRUSTED" => "INSTALLATION-SIGNATURE-REJECTED",
        "INSTALLATION-PENDING" => "INSTALLATION-RECOVERY-REQUIRED",
        _ => "INSTALLATION-REVIEW-REQUIRED"
    };
}

public enum EnrollmentFaultPoint { PreparedRecordCreated, RecordFlushed, BeforeActivation, Activated }
