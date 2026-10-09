using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public enum PurgeSupportState
{
    NotProbed,
    CandidateOnly,
    VerifiedReadOnly,
    Unsupported
}

public sealed class PurgeMethodReadiness
{
    public StorageSanitizeMethod Method { get; init; }
    public PurgeSupportState State { get; init; }
    public string Detail { get; init; } = "";
    public bool CanExecute => false;
}

public sealed class StoragePurgeReadinessReport
{
    public string DeviceFingerprint { get; init; } = "";
    public bool RequiresWinPe { get; init; }
    public bool IsWinPe { get; init; }
    public bool ExecutionEnabled => false;
    public PurgeReadOnlyInspection? ReadOnlyInspection { get; init; }
    public IReadOnlyList<PurgeMethodReadiness> Methods { get; init; } = Array.Empty<PurgeMethodReadiness>();
    public IReadOnlyList<string> RequiredFutureChecks { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Reports only what can be established without sending a storage command. Candidate is not hardware verification.
/// Future executors must obtain a fresh, read-only Identify/feature report in WinPE before any destructive action.
/// </summary>
public sealed class StoragePurgeReadinessService
{
    public StoragePurgeReadinessReport Evaluate(StorageSanitizeDryRunResult plan, PurgeReadOnlyInspection? inspection = null, NvmeIdentifyReadOnlyEvidence? nvmeIdentify = null)
    {
        var now = DateTimeOffset.UtcNow;
        var isNvme = string.Equals(plan.Device.BusType, "NVMe", StringComparison.OrdinalIgnoreCase);
        var inspectionCurrent = inspection?.IsFresh(now) == true &&
            DeviceFingerprint.FixedTimeEquals(DeviceFingerprint.Create(plan.Device), inspection.DeviceFingerprint);
        var nvmeCurrent = nvmeIdentify?.IsCurrentFor(plan, now) == true;
        var methods = new List<PurgeMethodReadiness>
        {
            new()
            {
                Method = StorageSanitizeMethod.CryptoErase,
                State = nvmeCurrent ? (nvmeIdentify!.SupportsCryptoErase ? PurgeSupportState.VerifiedReadOnly : PurgeSupportState.Unsupported) : isNvme ? PurgeSupportState.CandidateOnly : PurgeSupportState.NotProbed,
                Detail = nvmeCurrent
                    ? nvmeIdentify!.SupportsCryptoErase ? "NVMe Identify SANICAP Crypto Erase 비트를 읽기 전용으로 확인했습니다. 실행 승인·삭제 완료 증명은 아닙니다." : "NVMe Identify SANICAP은 Crypto Erase 미지원으로 보고했습니다."
                    : isNvme ? "NVMe 버스가 감지되었습니다. Sanitize/Crypto Erase 지원 여부는 아직 확인하지 않았습니다." : "NVMe Identify/지원 효과 로그를 읽지 않아 지원 여부를 확인하지 않았습니다."
            },
            new()
            {
                Method = StorageSanitizeMethod.BlockErase,
                State = nvmeCurrent ? (nvmeIdentify!.SupportsBlockErase ? PurgeSupportState.VerifiedReadOnly : PurgeSupportState.Unsupported) : isNvme ? PurgeSupportState.CandidateOnly : PurgeSupportState.NotProbed,
                Detail = nvmeCurrent
                    ? nvmeIdentify!.SupportsBlockErase ? "NVMe Identify SANICAP Block Erase 비트를 읽기 전용으로 확인했습니다. 실행 승인·삭제 완료 증명은 아닙니다." : "NVMe Identify SANICAP은 Block Erase 미지원으로 보고했습니다."
                    : isNvme ? "NVMe Block Erase 후보입니다. 실제 지원 여부는 WinPE의 읽기 전용 확인이 필요합니다." : "장치 명령 지원 여부 미확인"
            },
            new()
            {
                Method = StorageSanitizeMethod.Overwrite,
                State = PurgeSupportState.NotProbed,
                Detail = "장치 전체 덮어쓰기는 매체 유형·용량·전원 안정성·검증 정책이 확정되기 전까지 계획하지 않습니다."
            },
            new()
            {
                Method = StorageSanitizeMethod.BitLockerKeyDestroy,
                State = PurgeSupportState.NotProbed,
                Detail = "BitLocker 키 파기는 복구 키·조직 정책·부팅 복구 영향 검증 전까지 후보로도 승인하지 않습니다."
            }
        };

        return new StoragePurgeReadinessReport
        {
            DeviceFingerprint = DeviceFingerprint.Create(plan.Device),
            RequiresWinPe = plan.RequiresWinPe,
            IsWinPe = plan.IsWinPe,
            ReadOnlyInspection = inspection,
            Methods = methods,
            RequiredFutureChecks =
            [
                "WinPE에서 대상 Disk 번호·고유 ID·크기·버스 타입을 다시 대조",
                inspectionCurrent ? "읽기 전용 재식별 완료: 펌웨어 명령 지원 값은 별도로 확인 필요" : "5분 이내의 읽기 전용 재식별 증적이 없습니다.",
                nvmeCurrent ? "NVMe Identify SANICAP 읽기 전용 증적을 장치 지문에 연결했습니다." : "5분 이내의 NVMe Identify SANICAP 증적이 없습니다.",
                "NVMe Identify 및 Command Supported and Effects Log를 읽기 전용으로 검증",
                "볼륨 잠금·캐시 플러시·전원 안정성·복구 키 보관 여부를 별도 확인",
                "승인 토큰, 장치 지문, WinPE 재식별이 모두 일치할 때만 미래 실행기를 고려"
            ]
        };
    }
}

/// <summary>Creates a non-executable, human-reviewable WinPE preparation runbook. It never creates boot media or changes BCD.</summary>
public sealed class StoragePurgeWinPePreparationService
{
    public WinPePreparationReceipt Create(StorageSanitizeDryRunResult plan, PurgeAuthorizationDecision authorization, string outputDirectory)
    {
        if (!authorization.AuthorizedForFutureExecutor) throw new InvalidOperationException("유효한 일회용 승인 없이는 WinPE 준비 문서를 만들 수 없습니다.");
        if (!plan.Device.IsResolved) throw new InvalidOperationException("물리 디스크 식별이 완료되지 않았습니다.");
        if (!string.Equals(authorization.DeviceFingerprint, DeviceFingerprint.Create(plan.Device), StringComparison.Ordinal)) throw new InvalidOperationException("승인 토큰과 현재 장치 지문이 일치하지 않습니다.");
        if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("출력 폴더가 필요합니다.", nameof(outputDirectory));

        Directory.CreateDirectory(outputDirectory);
        var runbook = new WinPePreparationRunbook
        {
            Version = "pccare-purge-prep-v1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TargetFingerprint = authorization.DeviceFingerprint,
            TargetDisplay = plan.Device.DisplayId,
            RequiresWinPe = plan.RequiresWinPe,
            ExecutionEnabled = false,
            DeviceCommandsIncluded = false,
            Steps =
            [
                "WinPE로 부팅한 뒤 대상 Disk 번호·고유 ID·크기·버스 타입을 재확인합니다.",
                "지원 여부는 읽기 전용 Identify/효과 로그로만 판단합니다.",
                "이 준비 문서는 실행 명령, 부팅 설정 변경, 디스크 잠금 또는 삭제 명령을 포함하지 않습니다.",
                "향후 별도 실행기가 도입되더라도 새 승인·새 Dry Run·새 WinPE 재식별을 요구해야 합니다."
            ]
        };
        var path = Path.Combine(outputDirectory, "pccare-purge-preparation-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(runbook, new JsonSerializerOptions { WriteIndented = true }));
        return new WinPePreparationReceipt { Path = path, ExecutionEnabled = false, DeviceCommandsIncluded = false };
    }
}

public sealed class WinPePreparationRunbook
{
    public string Version { get; init; } = "";
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string TargetFingerprint { get; init; } = "";
    public string TargetDisplay { get; init; } = "";
    public bool RequiresWinPe { get; init; }
    public bool ExecutionEnabled { get; init; }
    public bool DeviceCommandsIncluded { get; init; }
    public string[] Steps { get; init; } = Array.Empty<string>();
}

public sealed class WinPePreparationReceipt
{
    public string Path { get; init; } = "";
    public bool ExecutionEnabled { get; init; }
    public bool DeviceCommandsIncluded { get; init; }
}