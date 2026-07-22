namespace SmartPerformanceDoctor.App.Models.Security;

public enum SecureDeleteTargetType
{
    File,
    Directory
}

public enum SecureDeleteValidationStatus
{
    Valid,
    NotFound,
    WrongType,
    Blocked,
    Inaccessible
}

public sealed record SecureDeleteSelection(
    string OriginalPath,
    string NormalizedPath,
    SecureDeleteTargetType TargetType,
    bool Exists,
    bool IsReparsePoint,
    SecureDeleteValidationStatus ValidationStatus,
    string? ValidationMessage)
{
    public string DisplayLine =>
        $"{(TargetType == SecureDeleteTargetType.Directory ? "폴더" : "파일")} · {OriginalPath}";
}

public sealed record SecureDeleteSelectionResult(
    bool Added,
    SecureDeleteSelection? Selection,
    string Message,
    int RemovedChildren = 0);
