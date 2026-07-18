namespace SmartPerformanceDoctor.Contracts.Models.Installation;

/// <summary>
/// 설치 UI에서 사용자가 선택하는 설치 프리셋.
/// </summary>
public enum InstallMode
{
    /// <summary>권장 구성: 필수 + 일반 사용자에게 유용한 선택 기능.</summary>
    Recommended,

    /// <summary>전체 구성: 필수 + 모든 선택 기능.</summary>
    Full,

    /// <summary>사용자가 기능별로 직접 선택.</summary>
    Custom,

    /// <summary>최소 구성: 앱 실행에 필요한 필수 기능만.</summary>
    Minimal
}