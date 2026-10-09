using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>
/// One-time, in-memory authorization for a future Purge executor. It cannot execute a disk command.
/// The authorization is bound to a physical-device fingerprint, expires quickly, and is consumed once.
/// </summary>
public sealed class StoragePurgeAuthorizationService
{
    private readonly Dictionary<string, PendingAuthorization> _pending = new(StringComparer.Ordinal);
    private readonly Func<DateTimeOffset> _utcNow;

    public StoragePurgeAuthorizationService(Func<DateTimeOffset>? utcNow = null) => _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    public PurgeAuthorizationChallenge Begin(StorageSanitizeDryRunResult plan)
    {
        if (!CanAuthorize(plan, out var reason)) throw new InvalidOperationException(reason);
        var deviceFingerprint = DeviceFingerprint.Create(plan.Device);
        var operationId = "purge-auth-" + Guid.NewGuid().ToString("N");
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
        var expires = _utcNow().AddMinutes(10);
        _pending[operationId] = new PendingAuthorization(deviceFingerprint, code, expires);
        return new PurgeAuthorizationChallenge
        {
            OperationId = operationId,
            DeviceFingerprint = deviceFingerprint,
            DisplayTarget = plan.Device.DisplayId,
            ChallengeCode = code,
            ExpiresAtUtc = expires,
            RequiredPhrase = $"PURGE DISK {plan.Device.DiskNumber} {code}",
            ExecutionEnabled = false,
            Notice = "이 확인은 향후 WinPE 실행 단계의 사전 승인 토큰일 뿐입니다. 현재 버전은 장치 Purge를 실행하지 않습니다."
        };
    }

    public PurgeAuthorizationDecision Confirm(StorageSanitizeDryRunResult currentPlan, string operationId, string typedPhrase)
    {
        if (!_pending.Remove(operationId, out var pending)) return Denied("확인 요청이 없거나 이미 사용되었습니다.");
        if (pending.ExpiresAtUtc < _utcNow()) return Denied("확인 요청이 만료되었습니다. 다시 Dry Run을 수행하세요.");
        if (!CanAuthorize(currentPlan, out var reason)) return Denied(reason);
        var currentFingerprint = DeviceFingerprint.Create(currentPlan.Device);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pending.DeviceFingerprint), Encoding.UTF8.GetBytes(currentFingerprint))) return Denied("Dry Run 이후 물리 디스크 식별값이 바뀌었습니다.");
        var expected = $"PURGE DISK {currentPlan.Device.DiskNumber} {pending.ChallengeCode}";
        if (!string.Equals(typedPhrase?.Trim(), expected, StringComparison.Ordinal)) return Denied("디스크 번호와 일회용 확인 코드를 포함한 확인 문구가 일치하지 않습니다.");
        return new PurgeAuthorizationDecision
        {
            AuthorizedForFutureExecutor = true,
            ExecutionEnabled = false,
            DeviceFingerprint = currentFingerprint,
            Message = "이중 식별·일회용 확인을 통과했습니다. 현재 버전에는 Purge 실행기가 없으므로 어떤 장치 명령도 수행되지 않았습니다."
        };
    }

    private static bool CanAuthorize(StorageSanitizeDryRunResult plan, out string reason)
    {
        if (!plan.Device.IsResolved) { reason = "물리 디스크를 식별하지 못해 확인을 시작할 수 없습니다."; return false; }
        if (plan.IsSystemDisk && !plan.IsWinPe) { reason = "부팅/시스템 디스크는 WinPE에서만 향후 확인 절차를 시작할 수 있습니다."; return false; }
        if (plan.Device.IsOffline || plan.Device.IsReadOnly) { reason = "오프라인 또는 읽기 전용 디스크는 확인할 수 없습니다."; return false; }
        if (plan.BlockingReasons.Count > 0) { reason = "Dry Run 차단 사유를 먼저 해소해야 합니다: " + string.Join(" ", plan.BlockingReasons); return false; }
        reason = ""; return true;
    }

    private static PurgeAuthorizationDecision Denied(string message) => new() { AuthorizedForFutureExecutor = false, ExecutionEnabled = false, Message = message };

    private sealed record PendingAuthorization(string DeviceFingerprint, string ChallengeCode, DateTimeOffset ExpiresAtUtc);
}

public sealed class PurgeAuthorizationChallenge
{
    public string OperationId { get; init; } = "";
    public string DeviceFingerprint { get; init; } = "";
    public string DisplayTarget { get; init; } = "";
    public string ChallengeCode { get; init; } = "";
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public string RequiredPhrase { get; init; } = "";
    public bool ExecutionEnabled { get; init; }
    public string Notice { get; init; } = "";
}

public sealed class PurgeAuthorizationDecision
{
    public bool AuthorizedForFutureExecutor { get; init; }
    public bool ExecutionEnabled { get; init; }
    public string DeviceFingerprint { get; init; } = "";
    public string Message { get; init; } = "";
}

public static class DeviceFingerprint
{
    public static string Create(StorageDeviceIdentity device)
    {
        if (!device.IsResolved) return "";
        var source = string.Join("|", "pccare-purge-v1", device.DiskNumber, device.UniqueId, device.SerialNumber, device.SizeBytes, device.BusType);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    public static bool FixedTimeEquals(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    }}
