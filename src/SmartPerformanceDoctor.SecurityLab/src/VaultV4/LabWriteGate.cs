namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Lab write authorization boundary (RO sessions cannot write).
/// Lab v5 remains a valid product write path even when AV3 gates are authorized.
/// </summary>
public static class LabWriteGate
{
    public sealed class Decision
    {
        public required bool Allowed { get; init; }
        public required string Reason { get; init; }
    }

    public static Decision Evaluate(bool vaultUnlocked, bool writeAllowed)
    {
        if (!vaultUnlocked)
        {
            return new Decision { Allowed = false, Reason = "금고가 잠겨 있습니다." };
        }

        if (!writeAllowed)
        {
            return new Decision { Allowed = false, Reason = "읽기 전용 세션에서는 쓰기·삭제가 불가합니다." };
        }

        return new Decision { Allowed = true, Reason = "Lab v5 write path" };
    }

    public static void EnsureAllowed(bool vaultUnlocked, bool writeAllowed)
    {
        var d = Evaluate(vaultUnlocked, writeAllowed);
        if (!d.Allowed)
        {
            throw new InvalidOperationException(d.Reason);
        }
    }
}
