namespace SmartPerformanceDoctor.UpdateEngine;

public static class UpdateStateText
{
    public static string Recovery(RecoveryReconciliation value) => value.State switch
    {
        "RecoveryDeferred" => "다른 프로그램이 파일을 사용 중입니다. 파일을 보존하며 잠금 해제 후 다시 검사하세요.",
        "RecoveryRequired" when value.OriginalBytesVerified => "원본 바이트를 확인했습니다. 미완료 거래의 종료 확인이 필요하며 복원 완료 상태는 아닙니다.",
        "RecoveryRequired" => "복구 입력을 확인했습니다. 관리자 승인 후 실행 직전에 다시 검증합니다.",
        "NoPendingTransaction" or "NotStarted" => "미완료 거래가 없습니다. 전체 설치 신뢰는 별도로 확인합니다.",
        _ => "복구 근거를 확인하지 못했습니다. 기존 파일과 기록을 보존하며 수동 확인이 필요합니다."
    };
    public static string Installation(string code) => code switch
    {
        "SIGNED-HISTORY-VERIFIED" => "서명된 설치 이력과 해당 파일의 내용·신원을 확인했습니다.",
        "INSTALLATION-HISTORY-INCOMPLETE" or "INSTALLATION-HISTORY-JOURNAL" => "설치 이력이 일부 누락됐습니다. 기존 파일을 보존하며 복구 자료를 확인하세요.",
        "INSTALLATION-HISTORY-CONTENT-MISMATCH" => "이전 업데이트 파일의 내용이 설치 이력과 다릅니다. 수동 확인이 필요합니다.",
        "INSTALLATION-UNATTESTED-EXECUTABLE" => "검증 목록에 없는 실행 파일이 있습니다. 파일을 보존하며 설치 출처를 확인하세요.",
        "INSTALLATION-HISTORY-SIGNATURE" => "이전 설치 선언의 서명을 확인하지 못했습니다. 일반 실행을 보류합니다.",
        "PUBLISH-SAME-VOLUME-REQUIRED" => "설치 위치와 업데이트 준비 위치의 볼륨이 다릅니다. 설치 파일 변경 전에 중단했습니다.",
        "INSTALLATION-CONTENT-MISMATCH" => "설치 파일 내용이 검증된 목록과 다릅니다. 파일과 복구 기록을 보존합니다.",
        "INSTALLATION-IDENTITY-MISMATCH" => "설치 파일 또는 폴더가 바뀌었습니다. 원래 대상을 다시 확인해야 합니다.",
        "INSTALLATION-ACL-MISMATCH" => "설치 위치의 접근 권한이 보호 정책과 다릅니다. 관리자 검토가 필요합니다.",
        "INSTALLATION-SIGNATURE-REJECTED" => "설치 선언의 발행자를 인증하지 못했습니다. 일반 실행을 보류합니다.",
        "INSTALLATION-NOT-ATTESTED" => "기존 설치에 서명된 검증 기록이 없습니다. 검증 완료 상태는 아닙니다.",
        "CANCELLED-PREPARATION" => "설치 파일을 변경하기 전에 준비를 취소했습니다.",
        _ => "설치 상태의 추가 확인이 필요합니다. 기존 파일과 기록을 보존합니다."
    };
    public static string Approval(LegacyApprovalObservation value) => value.State switch
    {
        LegacyApprovalState.RestorationCommitted => "복원 저널과 모든 대상 파일의 원본 해시를 확인했습니다. 결과 전달 여부는 별도로 확인합니다.",
        LegacyApprovalState.ApprovalRequested => "승인 예약이 없습니다. 새 관리자 승인 후에만 복원을 시작할 수 있습니다.",
        LegacyApprovalState.ApprovalReserved => "이전 승인이 소비됐지만 복원 시작은 확인되지 않았습니다. 대상 재검토와 새 승인이 필요합니다.",
        LegacyApprovalState.RestorationInterrupted => "복원 중단 기록을 확인했습니다. 이전 승인을 재사용하지 말고 관리자 복구로 이어가세요.",
        LegacyApprovalState.RestorationInProgress => "복원 작업이 실행 중입니다. 종료 후 상태를 다시 확인하세요.",
        _ => "복원 완료를 확인하지 못했습니다. 파일과 승인 기록을 보존하며 수동 검토가 필요합니다."
    };
}
