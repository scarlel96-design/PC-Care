namespace SmartPerformanceDoctor.Contracts.Models.Installation;

/// <summary>
/// 설치 UI 및 MSI 기능 트리에 표시할 기능 메타데이터.
/// </summary>
public sealed record FeatureDefinition
{
    public string Id { get; init; } = "";

    /// <summary>설치 UI 표시 이름 (한국어).</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>설치 UI 설명 (한국어).</summary>
    public string Description { get; init; } = "";

    /// <summary>UI 그룹 (예: core, rules, repair).</summary>
    public string Category { get; init; } = "";

    /// <summary>low | medium | high</summary>
    public string RiskLevel { get; init; } = "low";

    /// <summary>관리자 권한(UAC)이 필요한 기능인지.</summary>
    public bool RequiresElevation { get; init; }

    /// <summary>WiX MSI Feature Id.</summary>
    public string MsiFeatureName { get; init; } = "";

    /// <summary>해제 불가 필수 기능.</summary>
    public bool IsRequired { get; init; }

    /// <summary>권장 설치 프리셋에 포함.</summary>
    public bool IncludedInRecommended { get; init; }

    /// <summary>최소 설치 프리셋에 포함.</summary>
    public bool IncludedInMinimal { get; init; }

    /// <summary>선행 설치가 필요한 기능 ID.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
}