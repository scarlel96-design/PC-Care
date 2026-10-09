# 업데이트 보안 감사 및 Phase 1 수정

기존 `UpdatePackageInspector`는 `packageSha256`과 파일 목록의 지문을 비교했다. 두 값이 일치하면 ZIP 안 실제 파일의 SHA-256을 읽지 않고 `READY`로 끝났다. `UpdateInstallerService`는 `inspection.IsValid`를 이유로 검증을 생략하고 ZIP 항목 이름에서 직접 목적 경로를 만들었다. 독립 프로브의 기존 결과는 `valid=READY;tampered=READY;traversal=READY`이다.

현재 패치는 매니페스트 항목의 중복/경로/해시 형식을 검사하고 모든 ZIP 파일의 실제 SHA-256을 스트리밍 검증한다. 선언되지 않은 파일, 누락·중복 항목, 경로 이탈, Windows 예약 이름, 항목 수·크기 제한 위반을 거부한다. 기존 패키지의 목록 지문 방식이 다르면 실제 파일 해시로 검증한다. 적용 시 같은 패키지를 재검사하고 검증된 목록만 GUID 스테이징 폴더로 제한 추출한 뒤 디스크의 파일을 다시 해시한다. 회귀 결과는 `valid=READY;tampered=CHECKSUM-FAIL;traversal=CHECKSUM-FAIL;extra=CHECKSUM-FAIL;duplicate=CHECKSUM-FAIL;reserved=CHECKSUM-FAIL`이다.

남은 범위: 메타데이터 자체의 신뢰 루트(릴리스 채널/서명된 manifest), 압축 해제와 설치 사이의 경합, 설치 폴더에 대한 원자적 커밋/롤백, 재부팅 대기 상태, 크기 제한의 제품 정책, 테스트 패키지와 실제 배포 패키지의 일치. `READY`는 콘텐츠 무결성 검증을 의미하며 발행자 인증을 의미하지 않는다. [Microsoft ZIP/TAR 보안 권장사항](https://learn.microsoft.com/en-us/dotnet/standard/io/zip-tar-best-practices) 및 [ZipArchiveEntry.FullName](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchiveentry.fullname?view=net-10.0)을 근거로 경로·크기·항목을 검사한다.

## 위협 모델과 생산 승인

공격자는 다운로드 후 ZIP 바이트를 바꾸거나, manifest 목록·ZIP 경로를 조작하거나, 스테이징 중 파일을 교체하거나, 재시작 직전/직후 적용을 중단할 수 있다. 데이터 손실은 실행 파일 부분 교체로 앱이 실행되지 않는 것과 사용자 데이터가 설치 대상에 잘못 복사되는 것이다. `packageSha256`은 현재 전체 ZIP 해시가 아니라 파일 목록 지문이다. 이 이름을 전체 패키지 인증으로 해석하지 않는다.

생산 승인에는 신뢰할 수 있는 릴리스 메타데이터와 실제 다운로드 해시의 바인딩, 임시 디렉터리 접근 제어, 원자적 버전 전환, 실패 후 이전 버전 복귀, 재시작 후 완료 상태, 클린 설치/증분 업데이트 실기기 테스트가 필요하다. 개발자 Authenticode 인증서 유무와 별개로 신뢰 상태는 `검증됨/서명 없음 허용/미검증/실패`로 표시하고 [WinVerifyTrust](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust) 결과와 정책 허용을 혼합하지 않는다.
