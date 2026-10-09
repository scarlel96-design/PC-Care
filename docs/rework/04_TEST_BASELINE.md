# 테스트 기준선

기준 커밋 `95eafbfebe6189afa81c873e58ab65f1d29e50e0`. 기존 검사기 복사본 SHA-256: `B0624CB57C6DF8580AA87041B018B01E14E9C29DE270F06E844A7AB5A743360C`.

독립 프로브 `tests/UpdateInspectorProbe`는 실제 `UpdatePackageInspector` 소스를 링크한다. 정상 ZIP, 실제 바이트 변조, `payload/../escape.dll`, 선언되지 않은 ZIP 파일, 중복 ZIP 파일, Windows 예약 이름 `CON.txt`를 만든다. 기존 코드 결과: `valid=READY;tampered=READY;traversal=READY;extra=READY;duplicate=READY;reserved=READY`. 수정 코드 결과: `valid=READY;tampered=CHECKSUM-FAIL;traversal=CHECKSUM-FAIL;extra=CHECKSUM-FAIL;duplicate=CHECKSUM-FAIL;reserved=CHECKSUM-FAIL`. 두 실행 모두 기대 판정과 일치해 종료 코드 0이다. 상세 명령·롤백 증거는 `evidence/VERIFICATION.txt`.

전체 앱 빌드의 첫 실행은 Windows App SDK SelfContained가 지원 아키텍처를 요구하며 실패했다. `-p:Platform=x64` 재실행은 경고 0/오류 0으로 통과했다. 관련 기존 테스트 3개도 통과했다. 오래된 배포 패키지는 목록 지문이 현재 방식과 다르지만 실제 ZIP 파일 해시가 맞으면 허용한다. 이 프로브는 앱 전체 설치/업데이트의 대체물이 아니다.

다음 테스트 관문: 업데이트 복사 중 중단/전원 장애, 설치 폴더 권한 경합, 대소문자 중복, 링크 항목, 매우 큰 압축 해제, 재시작 후 복구, 기존 금고 파일 복호화 호환, 보안 삭제 하드링크와 재분석 지점.

시스템 케어 복원 충돌 회귀는 새 파일과 격리본이 모두 남는지를 테스트한다. 변경 전 상세 전체 모음은 750개 중 628개 통과, 6개 실패, 116개 건너뜀으로 종료 코드 1이었다. 실패 중 5개는 두 금고 테스트 클래스가 전역 `SPD_TEST_VAULT_ROOT`를 동시에 교체해 발생했고, 각 테스트는 단독 실행에서 통과했다. 두 클래스를 비병렬 컬렉션으로 묶었다. 나머지 한 개는 보조 Purge 준비 모듈이 주 탐색 영역으로 표시된 구성 오류였으며 이를 수정했다. 최종 검사기 수정 후 전체 모음은 750개 중 634개 통과, 실패 0개, 건너뜀 116개, 종료 코드 0이었다. 완료 출력은 `evidence/full-suite-final.txt`에 있다.
