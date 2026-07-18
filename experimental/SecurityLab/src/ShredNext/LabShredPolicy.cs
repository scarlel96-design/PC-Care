namespace SmartPerformanceDoctor.SecurityLab.ShredNext;

/// <summary>Commercial shred policy sketch — not linked to product UI.</summary>
public static class LabShredPolicy
{
    public const string IrreversiblePhrase = "이 작업은 되돌릴 수 없습니다";

    public static bool IsConfirmPhraseValid(string input) =>
        string.Equals(input.Trim(), IrreversiblePhrase, StringComparison.Ordinal)
        || string.Equals(input.Trim(), "보안 삭제에 동의합니다", StringComparison.Ordinal);

    public static bool IsSystemPathBlocked(string fullPath)
    {
        var segments = fullPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2)
        {
            var first = segments[1];
            if (first.Equals("Windows", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Program Files", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase)
                || first.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
                || first.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Recovery", StringComparison.OrdinalIgnoreCase)
                || first.Equals("Boot", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (first.Equals("ProgramData", StringComparison.OrdinalIgnoreCase)
                && segments.Length >= 3
                && segments[2].Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return fullPath.Contains(@"\Windows\System32", StringComparison.OrdinalIgnoreCase)
               || fullPath.Contains(@"\Windows\SysWOW64", StringComparison.OrdinalIgnoreCase)
               || fullPath.Contains(@"\WinSxS\", StringComparison.OrdinalIgnoreCase);
    }
}
